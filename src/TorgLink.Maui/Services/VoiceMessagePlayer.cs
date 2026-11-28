using System.Threading;
using Concentus;
using Concentus.Oggfile;
using Microsoft.Extensions.Logging;

namespace TorgLink.Maui.Services;

/// <summary>In-app playback for downloaded voice (.ogg / Opus) attachments.</summary>
internal static class VoiceMessagePlayer
{
    private const int OpusDecodeSampleRate = 48_000;
    private const int MinAudiblePeak = 8;

    private static int _playGeneration;
    private static readonly SemaphoreSlim PlayGate = new(1, 1);

#if WINDOWS
    private static global::Windows.Media.Playback.MediaPlayer? _windowsPlayer;
#elif ANDROID
    private static Android.Media.MediaPlayer? _androidPlayer;
    private static Android.Media.AudioTrack? _androidTrack;
    private static CancellationTokenSource? _androidTrackCts;
    private static TaskCompletionSource<bool>? _androidPrepareTcs;
#endif

    public static async Task PlayAsync(byte[] oggBytes, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(oggBytes);
        if (oggBytes.Length == 0)
            throw new InvalidOperationException("Пустое голосовое сообщение.");

        await PlayGate.WaitAsync().ConfigureAwait(false);
        var generation = Interlocked.Increment(ref _playGeneration);
        try
        {
#if WINDOWS
            await PlayWindowsAsync(oggBytes, generation).ConfigureAwait(false);
#elif ANDROID
            await PlayAndroidAsync(oggBytes, logger, generation).ConfigureAwait(false);
#else
            await Task.CompletedTask.ConfigureAwait(false);
            throw new PlatformNotSupportedException("Воспроизведение голосовых на этой платформе не поддерживается.");
#endif
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Voice playback failed");
            throw;
        }
        finally
        {
            PlayGate.Release();
        }
    }

    public static void Stop()
    {
        Interlocked.Increment(ref _playGeneration);
#if WINDOWS
        try
        {
            _windowsPlayer?.Pause();
            _windowsPlayer?.Dispose();
        }
        catch
        {
            // ignore
        }

        _windowsPlayer = null;
#elif ANDROID
        CancelAndroidPrepare(new OperationCanceledException("Воспроизведение остановлено."));
        ReleaseAndroidTrack();
        ReleaseAndroidPlayer();
#endif
    }

#if WINDOWS
    private static async Task PlayWindowsAsync(byte[] oggBytes, int generation)
    {
        StopKeepingGate(generation);
        var wavPath = Path.Combine(FileSystem.CacheDirectory, $"voice_play_{Guid.NewGuid():N}.wav");
        try
        {
            var (pcm, sampleRate) = await Task.Run(() => DecodeOpusOggToPcm16(oggBytes)).ConfigureAwait(false);
            ThrowIfStale(generation);
            await File.WriteAllBytesAsync(wavPath, BuildWav(pcm, sampleRate)).ConfigureAwait(false);
            ThrowIfStale(generation);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                ThrowIfStale(generation);
                var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(wavPath);
                var player = new global::Windows.Media.Playback.MediaPlayer();
                _windowsPlayer = player;
                player.MediaEnded += (_, _) =>
                {
                    try
                    {
                        player.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }

                    if (ReferenceEquals(_windowsPlayer, player))
                        _windowsPlayer = null;
                    TryDelete(wavPath);
                };
                player.Source = global::Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
                player.Play();
            }).ConfigureAwait(false);
        }
        catch
        {
            TryDelete(wavPath);
            throw;
        }
    }

    private static void StopKeepingGate(int generation)
    {
        // Release previous player without bumping generation (caller already owns generation).
        try
        {
            _windowsPlayer?.Pause();
            _windowsPlayer?.Dispose();
        }
        catch
        {
            // ignore
        }

        _windowsPlayer = null;
        ThrowIfStale(generation);
    }
#elif ANDROID
    private static async Task PlayAndroidAsync(byte[] oggBytes, ILogger? logger, int generation)
    {
        // TRL-39: Concentus → PCM → AudioTrack (MediaPlayer often silent for Concentus Ogg).
        // Decode off UI; serialize sessions so Stop during PrepareAsync cannot hang forever.
        Exception? decodeError = null;
        byte[]? pcm = null;
        var sampleRate = OpusDecodeSampleRate;
        var peak = 0;
        try
        {
            var decoded = await Task.Run(() => DecodeOpusOggToPcm16(oggBytes)).ConfigureAwait(false);
            ThrowIfStale(generation);
            pcm = decoded.PcmBytes;
            sampleRate = decoded.SampleRateHz;
            peak = PeakAbs(pcm);
            logger?.LogInformation(
                "Voice PCM: bytes={Bytes} pcm={Pcm} rate={Rate} peak={Peak}",
                oggBytes.Length, pcm.Length, sampleRate, peak);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            decodeError = ex;
            logger?.LogDebug(ex, "Concentus PCM decode failed; trying native Ogg MediaPlayer");
        }

        if (pcm != null && peak >= MinAudiblePeak)
        {
            await PlayAndroidPcmAsync(pcm, sampleRate, generation).ConfigureAwait(false);
            return;
        }

        if (pcm != null && peak < MinAudiblePeak)
        {
            decodeError ??= new InvalidOperationException(
                "Голосовое сообщение без слышимого звука (тишина в данных).");
        }

        var oggPath = Path.Combine(FileSystem.CacheDirectory, $"voice_play_{Guid.NewGuid():N}.ogg");
        await File.WriteAllBytesAsync(oggPath, oggBytes).ConfigureAwait(false);
        ThrowIfStale(generation);
        try
        {
            await TryPlayAndroidOggAsync(oggPath, generation).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryDelete(oggPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(oggPath);
            throw decodeError ?? ex;
        }
    }

    private static Task TryPlayAndroidOggAsync(string oggPath, int generation)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            ThrowIfStale(generation);
            ReleaseAndroidTrack();
            ReleaseAndroidPlayer();

            var player = new Android.Media.MediaPlayer();
            _androidPlayer = player;
            _androidPrepareTcs = tcs;
            try
            {
                var attrs = new Android.Media.AudioAttributes.Builder()
                    .SetUsage(Android.Media.AudioUsageKind.Media)
                    .SetContentType(Android.Media.AudioContentType.Speech)
                    .Build();
                if (attrs != null)
                    player.SetAudioAttributes(attrs);
                player.SetDataSource(oggPath);
                player.Prepared += (_, _) =>
                {
                    try
                    {
                        if (!IsCurrentGeneration(generation) || !ReferenceEquals(_androidPlayer, player))
                        {
                            tcs.TrySetCanceled();
                            return;
                        }

                        player.Start();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                };
                player.Completion += (_, _) =>
                {
                    ReleasePlayerInstance(player, oggPath);
                };
                player.Error += (_, args) =>
                {
                    ReleasePlayerInstance(player, oggPath);
                    tcs.TrySetException(new InvalidOperationException(
                        $"MediaPlayer error what={args.What} extra={args.Extra}"));
                };
                player.PrepareAsync();
            }
            catch (Exception ex)
            {
                ReleasePlayerInstance(player, oggPath);
                tcs.TrySetException(ex);
            }

            try
            {
                await tcs.Task.ConfigureAwait(true);
            }
            finally
            {
                if (ReferenceEquals(_androidPrepareTcs, tcs))
                    _androidPrepareTcs = null;
            }
        });
    }

    private static void ReleasePlayerInstance(Android.Media.MediaPlayer player, string? oggPath)
    {
        try
        {
            player.Release();
            player.Dispose();
        }
        catch
        {
            // ignore
        }

        if (ReferenceEquals(_androidPlayer, player))
            _androidPlayer = null;
        if (oggPath != null)
            TryDelete(oggPath);
    }

    private static async Task PlayAndroidPcmAsync(byte[] pcm16, int sampleRateHz, int generation)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ThrowIfStale(generation);
            ReleaseAndroidTrack();
            ReleaseAndroidPlayer();

            var channel = Android.Media.ChannelOut.Mono;
            var encoding = Android.Media.Encoding.Pcm16bit;
            var minBuf = Android.Media.AudioTrack.GetMinBufferSize(sampleRateHz, channel, encoding);
            if (minBuf <= 0)
                throw new InvalidOperationException("AudioTrack buffer unavailable.");

            var attrs = new Android.Media.AudioAttributes.Builder()
                .SetUsage(Android.Media.AudioUsageKind.Media)
                .SetContentType(Android.Media.AudioContentType.Speech)
                .Build()!;
            var format = new Android.Media.AudioFormat.Builder()
                .SetSampleRate(sampleRateHz)
                .SetChannelMask(channel)
                .SetEncoding(encoding)
                .Build()!;

            var bufSize = Math.Max(minBuf * 2, 8192);
            var track = new Android.Media.AudioTrack.Builder()
                .SetAudioAttributes(attrs)
                .SetAudioFormat(format)
                .SetBufferSizeInBytes(bufSize)
                .SetTransferMode(Android.Media.AudioTrackMode.Stream)
                .Build();
            if (track.State != Android.Media.AudioTrackState.Initialized)
            {
                track.Release();
                track.Dispose();
                throw new InvalidOperationException("AudioTrack не инициализирован.");
            }

            _androidTrack = track;
            track.SetVolume(1f);
            track.Play();

            var cts = new CancellationTokenSource();
            _androidTrackCts = cts;
            var gen = generation;
            _ = Task.Run(() =>
            {
                try
                {
                    var offset = 0;
                    while (offset < pcm16.Length && !cts.IsCancellationRequested && IsCurrentGeneration(gen))
                    {
                        var written = track.Write(pcm16, offset, pcm16.Length - offset);
                        if (written < 0)
                            break;
                        if (written == 0)
                        {
                            Thread.Sleep(5);
                            continue;
                        }

                        offset += written;
                    }
                }
                catch
                {
                    // ignore
                }
                finally
                {
                    try
                    {
                        if (track.PlayState != Android.Media.PlayState.Stopped)
                            track.Stop();
                        track.Release();
                        track.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }

                    if (ReferenceEquals(_androidTrack, track))
                        _androidTrack = null;
                }
            }, cts.Token);
        }).ConfigureAwait(false);
    }

    private static int PeakAbs(byte[] pcm16)
    {
        var peak = 0;
        for (var i = 0; i + 1 < pcm16.Length; i += 2)
        {
            var s = (short)(pcm16[i] | (pcm16[i + 1] << 8));
            var a = Math.Abs((int)s);
            if (a > peak)
                peak = a;
        }

        return peak;
    }

    private static void CancelAndroidPrepare(Exception error)
    {
        var tcs = Interlocked.Exchange(ref _androidPrepareTcs, null);
        if (error is OperationCanceledException)
            tcs?.TrySetCanceled();
        else
            tcs?.TrySetException(error);
    }

    private static void ReleaseAndroidTrack()
    {
        try
        {
            _androidTrackCts?.Cancel();
            _androidTrackCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _androidTrackCts = null;

        try
        {
            _androidTrack?.Pause();
            _androidTrack?.Stop();
            _androidTrack?.Release();
            _androidTrack?.Dispose();
        }
        catch
        {
            // ignore
        }

        _androidTrack = null;
    }

    private static void ReleaseAndroidPlayer()
    {
        try
        {
            _androidPlayer?.Reset();
            _androidPlayer?.Release();
            _androidPlayer?.Dispose();
        }
        catch
        {
            // ignore
        }

        _androidPlayer = null;
    }
#endif

    private static bool IsCurrentGeneration(int generation) =>
        Volatile.Read(ref _playGeneration) == generation;

    private static void ThrowIfStale(int generation)
    {
        if (!IsCurrentGeneration(generation))
            throw new OperationCanceledException("Воспроизведение остановлено.");
    }

    private static (byte[] PcmBytes, int SampleRateHz) DecodeOpusOggToPcm16(byte[] oggBytes)
    {
        using var mem = new MemoryStream(oggBytes, false);
        var decoder = OpusCodecFactory.CreateDecoder(OpusDecodeSampleRate, 1);
        var oggIn = new OpusOggReadStream(decoder, mem);
        var samples = new List<short>();
        while (oggIn.HasNextPacket)
        {
            var pkt = oggIn.DecodeNextPacket();
            if (pkt is { Length: > 0 })
                samples.AddRange(pkt);
        }

        if (samples.Count == 0)
            throw new InvalidOperationException("Пустой Opus поток.");

        var pcm = new byte[samples.Count * 2];
        Buffer.BlockCopy(samples.ToArray(), 0, pcm, 0, pcm.Length);
        return (pcm, OpusDecodeSampleRate);
    }

    private static byte[] BuildWav(byte[] pcm16, int sampleRateHz)
    {
        using var ms = new MemoryStream(44 + pcm16.Length);
        using var bw = new BinaryWriter(ms);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + pcm16.Length);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRateHz);
        bw.Write(sampleRateHz * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(pcm16.Length);
        bw.Write(pcm16);
        return ms.ToArray();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}
