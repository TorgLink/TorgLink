using ShortP2P.Discovery;

namespace TorgLink.Maui.Services;

/// <summary>
/// Prepares outgoing video for the selected <see cref="TrafficQualityMode"/>.
/// Full re-encode currently works on Windows; other platforms pass through bytes.
/// </summary>
internal static class Video144pTranscoder
{
    /// <summary>
    /// AAC bitrate for economy video containers. Must stay in a range Media Foundation accepts;
    /// speech/Opus rates (8–12 kbit/s) are not valid here.
    /// </summary>
    private const uint TranscodeAacBitrateBps = 48_000;

    public static bool IsVideoMime(string mime) =>
        mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    public static async Task<(bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error)> PrepareAsync(
        string sourcePath, string sourceFileName, string sourceMime, TrafficQualityMode mode)
    {
        if (mode == TrafficQualityMode.Normal)
        {
            var raw = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
            return (true, raw, sourceMime, sourceFileName, null);
        }

#if WINDOWS
        return await TranscodeWindowsAsync(sourcePath, sourceFileName, mode).ConfigureAwait(false);
#else
        // No portable transcoder on Android/iOS yet — send original (size still enforced by caller).
        var bytes = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
        return (true, bytes, sourceMime, sourceFileName, null);
#endif
    }

#if WINDOWS
    private static async Task<(bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error)>
        TranscodeWindowsAsync(string sourcePath, string sourceFileName, TrafficQualityMode mode)
    {
        var (w, h) = mode.GetVideoResolution();
        var destName = Path.GetFileNameWithoutExtension(sourceFileName) + $"_{w}x{h}.mp4";
        string? lastError = null;

        try
        {
            var src = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
            var folder = await global::Windows.Storage.StorageFolder.GetFolderFromPathAsync(FileSystem.CacheDirectory);

            // 1) Mutate a stock MP4 profile in place (replacing Video with CreateH264() yields Unknown on many GPUs).
            // 2) Retry with hardware acceleration off.
            // 3) Stock QVGA profile + VideoTransformEffect OutputSize (scale in the graph).
            foreach (var attempt in BuildAttempts(w, h, mode.GetCameraVideoBitrate()))
            {
                var dest = await folder.CreateFileAsync(
                    $"torglink_vid_{DateTime.UtcNow.Ticks}.mp4",
                    global::Windows.Storage.CreationCollisionOption.ReplaceExisting);

                var transcoder = new global::Windows.Media.Transcoding.MediaTranscoder
                {
                    HardwareAccelerationEnabled = attempt.HardwareAcceleration
                };
                if (attempt.Transform is { } transform)
                    transcoder.AddVideoEffect(transform.ActivatableClassId, true, transform.Properties);

                var prepare = await transcoder.PrepareFileTranscodeAsync(src, dest, attempt.Profile);
                if (!prepare.CanTranscode)
                {
                    lastError = DescribeFailure(prepare.FailureReason, w, h, attempt.Label);
                    try
                    {
                        await dest.DeleteAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    continue;
                }

                await prepare.TranscodeAsync();
                var bytes = await File.ReadAllBytesAsync(dest.Path).ConfigureAwait(false);
                try
                {
                    File.Delete(dest.Path);
                }
                catch
                {
                    // ignore
                }

                if (bytes.Length == 0)
                {
                    lastError = $"Empty transcode output ({attempt.Label}).";
                    continue;
                }

                return (true, bytes, "video/mp4", destName, null);
            }

            return (false, null, "video/mp4", destName,
                lastError ?? $"Video transcode failed at {w}×{h}.");
        }
        catch (Exception ex)
        {
            return (false, null, "video/mp4", sourceFileName, ex.Message);
        }
    }

    private sealed record TranscodeAttempt(
        string Label,
        global::Windows.Media.MediaProperties.MediaEncodingProfile Profile,
        bool HardwareAcceleration,
        global::Windows.Media.Effects.VideoTransformEffectDefinition? Transform);

    private static IEnumerable<TranscodeAttempt> BuildAttempts(int width, int height, int videoBitrateBps)
    {
        yield return new TranscodeAttempt(
            "profile-hw",
            CreateSizedMp4Profile(width, height, videoBitrateBps),
            HardwareAcceleration: true,
            Transform: null);

        yield return new TranscodeAttempt(
            "profile-sw",
            CreateSizedMp4Profile(width, height, videoBitrateBps),
            HardwareAcceleration: false,
            Transform: null);

        var transform = new global::Windows.Media.Effects.VideoTransformEffectDefinition
        {
            OutputSize = new global::Windows.Foundation.Size(width, height)
        };
        yield return new TranscodeAttempt(
            "qvga+transform-hw",
            CreateQvgaAudioAdjustedProfile(videoBitrateBps),
            HardwareAcceleration: true,
            Transform: transform);

        yield return new TranscodeAttempt(
            "qvga+transform-sw",
            CreateQvgaAudioAdjustedProfile(videoBitrateBps),
            HardwareAcceleration: false,
            Transform: transform);
    }

    /// <summary>
    /// Start from a stock CreateMp4 preset and only tweak fields — do not replace <c>profile.Video</c>.
    /// </summary>
    private static global::Windows.Media.MediaProperties.MediaEncodingProfile CreateSizedMp4Profile(
        int width, int height, int videoBitrateBps)
    {
        var profile = global::Windows.Media.MediaProperties.MediaEncodingProfile.CreateMp4(
            global::Windows.Media.MediaProperties.VideoEncodingQuality.Qvga);
        if (profile.Video != null)
        {
            profile.Video.Width = (uint)width;
            profile.Video.Height = (uint)height;
            profile.Video.Bitrate = (uint)videoBitrateBps;
            profile.Video.FrameRate.Numerator = 15;
            profile.Video.FrameRate.Denominator = 1;
        }

        if (profile.Audio != null)
            profile.Audio.Bitrate = TranscodeAacBitrateBps;

        return profile;
    }

    private static global::Windows.Media.MediaProperties.MediaEncodingProfile CreateQvgaAudioAdjustedProfile(
        int videoBitrateBps)
    {
        var profile = global::Windows.Media.MediaProperties.MediaEncodingProfile.CreateMp4(
            global::Windows.Media.MediaProperties.VideoEncodingQuality.Qvga);
        if (profile.Video != null)
            profile.Video.Bitrate = (uint)videoBitrateBps;
        if (profile.Audio != null)
            profile.Audio.Bitrate = TranscodeAacBitrateBps;
        return profile;
    }

    private static string DescribeFailure(
        global::Windows.Media.Transcoding.TranscodeFailureReason reason,
        int width,
        int height,
        string attempt) =>
        reason switch
        {
            global::Windows.Media.Transcoding.TranscodeFailureReason.CodecNotFound =>
                $"Video codec not found ({attempt}).",
            global::Windows.Media.Transcoding.TranscodeFailureReason.InvalidProfile =>
                $"Invalid encoding profile ({width}×{height}, {attempt}).",
            _ =>
                $"Video transcode failed ({reason}) at {width}×{height} [{attempt}]."
        };
#endif
}
