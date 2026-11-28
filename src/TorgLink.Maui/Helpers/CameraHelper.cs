using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;
using Img = SixLabors.ImageSharp.Image;

namespace TorgLink.Maui.Helpers;

/// <summary>
/// A camera photo must be sent with normal left-to-right orientation.
/// The system camera may save the shot with orientation metadata (rotation /
/// selfie-preview mirror), and not every client applies it. On Windows,
/// CameraCaptureUI often returns already mirrored pixels for photo and video (TRL-38).
/// </summary>
public static class CameraHelper
{
    /// <summary>
    /// Returns the photo with orientation applied to the pixels and the orientation
    /// tag removed. When nothing needs changing or the file cannot be decoded, the
    /// original bytes are returned unchanged.
    /// </summary>
    public static byte[] Normalize(byte[] source)
    {
        try
        {
            using var image = Img.Load(source);
            var profile = image.Metadata.ExifProfile;
            var orientation = 1;
            if (profile is not null &&
                profile.TryGetValue(ExifTag.Orientation, out var orientationValue) &&
                orientationValue is not null)
            {
                orientation = orientationValue.Value;
            }

#if WINDOWS
            // TRL-38: Windows CameraCaptureUI returns mirrored pixels; flip after EXIF orient.
            if (orientation > 1)
                image.Mutate(x => x.AutoOrient());
            image.Mutate(x => x.Flip(FlipMode.Horizontal));
#else
            if (orientation <= 1)
                return source;

            // Apply the orientation to the pixels (AutoOrient rotates and un-mirrors),
            // then drop the tag so viewers do not rotate a second time.
            image.Mutate(x => x.AutoOrient());
#endif

            profile?.RemoveValue(ExifTag.Orientation);
            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms, new JpegEncoder { Quality = 92 });
            return ms.ToArray();
        }
        catch
        {
            // Could not decode — return as is; the send-time checks will filter junk out.
            return source;
        }
    }

#if WINDOWS
    /// <summary>
    /// TRL-38: undoes horizontal mirroring from Windows CameraCaptureUI video.
    /// Returns a new temp file path on success, or <paramref name="sourcePath"/> unchanged.
    /// </summary>
    public static async Task<string> UnmirrorVideoAsync(string sourcePath, CancellationToken ct = default)
    {
        try
        {
            var destPath = Path.Combine(
                FileSystem.CacheDirectory,
                $"torglink_unmirror_{DateTime.UtcNow.Ticks}{Path.GetExtension(sourcePath)}");
            if (string.IsNullOrEmpty(Path.GetExtension(destPath)))
                destPath += ".mp4";

            var src = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
            var folder = await global::Windows.Storage.StorageFolder.GetFolderFromPathAsync(
                Path.GetDirectoryName(destPath)!);
            var dest = await folder.CreateFileAsync(
                Path.GetFileName(destPath),
                global::Windows.Storage.CreationCollisionOption.ReplaceExisting);

            var profile = await global::Windows.Media.MediaProperties.MediaEncodingProfile
                .CreateFromFileAsync(src);
            if (profile?.Video is null)
                return sourcePath;

            var effect = new global::Windows.Media.Effects.VideoTransformEffectDefinition
            {
                Mirror = global::Windows.Media.MediaProperties.MediaMirroringOptions.Horizontal
            };

            var transcoder = new global::Windows.Media.Transcoding.MediaTranscoder();
            transcoder.AddVideoEffect(effect.ActivatableClassId, true, effect.Properties);

            var prepare = await transcoder.PrepareFileTranscodeAsync(src, dest, profile);
            if (!prepare.CanTranscode)
                return sourcePath;

            ct.ThrowIfCancellationRequested();
            await prepare.TranscodeAsync();

            var info = new FileInfo(dest.Path);
            return info.Exists && info.Length > 0 ? dest.Path : sourcePath;
        }
        catch
        {
            return sourcePath;
        }
    }
#endif
}
