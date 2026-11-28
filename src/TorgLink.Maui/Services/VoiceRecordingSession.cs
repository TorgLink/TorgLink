namespace TorgLink.Maui.Services;

internal sealed class VoiceRecordingResult
{
    public required byte[] Bytes { get; init; }
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
}

internal sealed class VoiceRecordingSession : IAsyncDisposable
{
    public const string OggMime = "audio/ogg";
    public const string OggFileName = "voice.ogg";

#if ANDROID
    private global::Android.Media.MediaRecorder? _androidRecorder;
#elif WINDOWS
    private NAudio.Wave.WaveInEvent? _waveIn;
    private NAudio.Wave.WaveFileWriter? _waveWriter;
    private MemoryStream? _waveMs;
    private readonly object _capLock = new();
    private byte[]? _wavBytes;
#elif IOS || MACCATALYST
    private AVFoundation.AVAudioRecorder? _iosRecorder;
#endif
    private string? _tempPath;
    private bool _recording;
    private int _speechBitrateBps = MediaEconomy.DefaultSpeechBitrateBps;

    public bool IsRecording => _recording;

    public async Task StartAsync(int speechBitrateBps = MediaEconomy.DefaultSpeechBitrateBps)
    {
        if (_recording)
            return;
        _speechBitrateBps = Math.Clamp(speechBitrateBps, MediaEconomy.MinVoiceBitrateBps, 64_000);

#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            throw new InvalidOperationException("Голосовые сообщения требуют Android 10+ (API 29).");

        _tempPath = Path.Combine(FileSystem.CacheDirectory, $"voice_{DateTime.UtcNow.Ticks}.ogg");
        var recorder = new global::Android.Media.MediaRecorder();
        _androidRecorder = recorder;
        recorder.SetAudioSource(global::Android.Media.AudioSource.Mic);
        recorder.SetOutputFormat(global::Android.Media.OutputFormat.Ogg);
        recorder.SetAudioEncoder(global::Android.Media.AudioEncoder.Opus);
        recorder.SetAudioEncodingBitRate(_speechBitrateBps);
        recorder.SetAudioSamplingRate(48_000);
        recorder.SetOutputFile(_tempPath);
        recorder.Prepare();
        recorder.Start();
        _recording = true;
        await Task.CompletedTask.ConfigureAwait(false);
#elif WINDOWS
        await MainThread.InvokeOnMainThreadAsync(StartWindowsWaveIn).ConfigureAwait(false);
#elif IOS || MACCATALYST
        StartIos();
        await Task.CompletedTask.ConfigureAwait(false);
#else
        throw new NotSupportedException("Запись голоса на этой платформе не поддерживается.");
#endif
    }

    public Task<VoiceRecordingResult> StopAsync() => StopAndTakeResultAsync();

    /// <summary>Stops the microphone. Leaves the temp file for <see cref="TakeResultAsync"/>.</summary>
    public async Task StopCaptureAsync()
    {
        if (!_recording)
            throw new InvalidOperationException("Recording is not active.");

#if ANDROID
        try
        {
            _androidRecorder?.Stop();
        }
        finally
        {
            ReleaseAndroid();
            _recording = false;
        }

        await Task.CompletedTask.ConfigureAwait(false);
#elif WINDOWS
        await MainThread.InvokeOnMainThreadAsync(StopWindowsWaveIn).ConfigureAwait(false);
        _recording = false;
#elif IOS || MACCATALYST
        StopIos();
        _recording = false;
        await Task.CompletedTask.ConfigureAwait(false);
#else
        throw new NotSupportedException("Запись голоса на этой платформе не поддерживается.");
#endif
    }

    /// <summary>Reads / encodes the captured file. Safe to run off the UI thread.</summary>
    public Task<VoiceRecordingResult> TakeResultAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if ANDROID
        return ReadOggTempAsync(cancellationToken);
#elif WINDOWS
        return Task.FromResult(EncodeWindowsWavToOgg(_speechBitrateBps));
#else
        return Task.FromResult(EncodeWavTempToOgg(_speechBitrateBps));
#endif
    }

    private async Task<VoiceRecordingResult> StopAndTakeResultAsync()
    {
        await StopCaptureAsync().ConfigureAwait(false);
        return await TakeResultAsync().ConfigureAwait(false);
    }

    public async Task DiscardAsync()
    {
        try
        {
#if ANDROID
            try
            {
                _androidRecorder?.Stop();
            }
            catch
            {
                // ignore invalid state
            }

            ReleaseAndroid();
#elif WINDOWS
            await MainThread.InvokeOnMainThreadAsync(StopWindowsWaveIn).ConfigureAwait(false);
            _wavBytes = null;
#elif IOS || MACCATALYST
            StopIos();
#endif
        }
        catch
        {
            // ignore
        }
        finally
        {
            _recording = false;
            DeleteTemp();
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => new(DiscardAsync());

#if WINDOWS
    private void StartWindowsWaveIn()
    {
        _wavBytes = null;
        _waveMs = new MemoryStream();
        // 16 kHz mono PCM — same capture format as the working desktop client path.
        var wf = new NAudio.Wave.WaveFormat(16_000, 16, 1);
        _waveWriter = new NAudio.Wave.WaveFileWriter(_waveMs, wf);
        _waveIn = new NAudio.Wave.WaveInEvent
        {
            WaveFormat = wf,
            BufferMilliseconds = 100
        };
        _waveIn.DataAvailable += OnWindowsDataAvailable;
        _waveIn.StartRecording();
        _recording = true;
    }

    private void OnWindowsDataAvailable(object? sender, NAudio.Wave.WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
            return;
        lock (_capLock)
        {
            try
            {
                _waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            }
            catch
            {
                // disposed
            }
        }
    }

    private void StopWindowsWaveIn()
    {
        try
        {
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnWindowsDataAvailable;
                try
                {
                    _waveIn.StopRecording();
                }
                catch
                {
                    // ignore
                }

                _waveIn.Dispose();
            }
        }
        finally
        {
            _waveIn = null;
            lock (_capLock)
            {
                try
                {
                    _waveWriter?.Dispose();
                }
                catch
                {
                    // ignore
                }

                _waveWriter = null;
                try
                {
                    // MemoryStream.ToArray works after WaveFileWriter closes the stream.
                    _wavBytes = _waveMs?.ToArray();
                }
                catch
                {
                    _wavBytes = null;
                }

                try
                {
                    _waveMs?.Dispose();
                }
                catch
                {
                    // ignore
                }

                _waveMs = null;
            }
        }
    }

    private VoiceRecordingResult EncodeWindowsWavToOgg(int bitrateBps)
    {
        var wav = _wavBytes;
        _wavBytes = null;
        if (wav == null || wav.Length < 44)
            throw new InvalidOperationException("Не удалось получить записанный голосовой файл.");
        var bytes = OggOpusEncoder.EncodeWavPcm(wav, bitrateBps);
        return new VoiceRecordingResult { Bytes = bytes, FileName = OggFileName, MimeType = OggMime };
    }
#endif

#if IOS || MACCATALYST
    private void StartIos()
    {
        var session = AVFoundation.AVAudioSession.SharedInstance();
        session.SetCategory(AVFoundation.AVAudioSessionCategory.PlayAndRecord,
            AVFoundation.AVAudioSessionCategoryOptions.DefaultToSpeaker, out var catErr);
        if (catErr != null)
            throw new InvalidOperationException(catErr.LocalizedDescription);
        session.SetActive(true, out var actErr);
        if (actErr != null)
            throw new InvalidOperationException(actErr.LocalizedDescription);

        _tempPath = Path.Combine(FileSystem.CacheDirectory, $"voice_{DateTime.UtcNow.Ticks}.wav");
        var url = Foundation.NSUrl.FromFilename(_tempPath);
        var settings = new AVFoundation.AudioSettings
        {
            Format = AudioToolbox.AudioFormatType.LinearPCM,
            SampleRate = 48_000,
            NumberChannels = 1,
            AudioQuality = AVFoundation.AVAudioQuality.Medium,
            LinearPcmBitDepth = 16,
            LinearPcmFloat = false,
            LinearPcmBigEndian = false
        };
        var recorder = AVFoundation.AVAudioRecorder.Create(url, settings, out var error);
        if (recorder == null)
            throw new InvalidOperationException(error?.LocalizedDescription ?? "AVAudioRecorder failed.");
        if (!recorder.PrepareToRecord() || !recorder.Record())
            throw new InvalidOperationException("Не удалось начать запись.");
        _iosRecorder = recorder;
        _recording = true;
    }

    private void StopIos()
    {
        try
        {
            _iosRecorder?.Stop();
            _iosRecorder?.Dispose();
        }
        finally
        {
            _iosRecorder = null;
        }
    }
#endif

#if ANDROID
    private void ReleaseAndroid()
    {
        try
        {
            _androidRecorder?.Release();
            _androidRecorder?.Dispose();
        }
        catch
        {
            // ignore
        }

        _androidRecorder = null;
    }
#endif

    private async Task<VoiceRecordingResult> ReadOggTempAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_tempPath) || !File.Exists(_tempPath))
            throw new InvalidOperationException("Не удалось получить записанный голосовой файл.");
        var bytes = await File.ReadAllBytesAsync(_tempPath, cancellationToken).ConfigureAwait(false);
        DeleteTemp();
        if (bytes.Length == 0)
            throw new InvalidOperationException("Голосовая запись пустая.");
        return new VoiceRecordingResult { Bytes = bytes, FileName = OggFileName, MimeType = OggMime };
    }

    private VoiceRecordingResult EncodeWavTempToOgg(int bitrateBps)
    {
        if (string.IsNullOrEmpty(_tempPath) || !File.Exists(_tempPath))
            throw new InvalidOperationException("Не удалось получить записанный голосовой файл.");
        try
        {
            var bytes = OggOpusEncoder.EncodeWavPcm(_tempPath, bitrateBps);
            if (bytes.Length == 0)
                throw new InvalidOperationException("Голосовая запись пустая.");
            return new VoiceRecordingResult { Bytes = bytes, FileName = OggFileName, MimeType = OggMime };
        }
        finally
        {
            DeleteTemp();
        }
    }

    private void DeleteTemp()
    {
        if (string.IsNullOrEmpty(_tempPath))
            return;
        try
        {
            File.Delete(_tempPath);
        }
        catch
        {
            // ignore
        }

        _tempPath = null;
    }
}
