using Microsoft.Extensions.Logging;
using NAudio.Wave;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using TorgLink.Localization;
using Timer = System.Windows.Forms.Timer;

namespace TorgLink.WinForms;

public sealed partial class ChatForm
{
    private int MaxVoiceRecordSeconds => MediaEconomy.VoiceMaxSeconds(_routing);

    private readonly object _voiceCapLock = new();
    private volatile bool _voiceDiscardNextStop;
    private DateTime _voiceRecordStartUtc;
    private Timer? _voiceRecordTimer;
    private WaveInEvent? _voiceWaveIn;
    private MemoryStream? _voiceWaveMs;
    private WaveFileWriter? _voiceWaveWriter;

    private async Task OnAttachImageAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        EnsureSession(user);
        if (_p2pSession == null)
            return;

        if (BlockBinaryOnMeshtastic("image"))
            return;

        using var dlg = new OpenFileDialog
        {
            Title = "Изображение (JPEG, PNG, GIF, до 10 МБ)",
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.gif|Все файлы|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dlg.FileNames)
            await SendPickedImageAsync(path).ConfigureAwait(true);
    }

    private async Task SendPickedImageAsync(string path)
    {
        try
        {
            if (!ImageAttachHelper.TryGetMimeFromExtension(path, out var mime))
            {
                MessageBox.Show(this, "Допустимы только .jpg, .jpeg, .png, .gif.", "Файл",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var bytes = await ReadAllBytesAsync(path).ConfigureAwait(true);
            if (bytes.Length < 12)
            {
                MessageBox.Show(this, "Файл слишком маленький.", "Файл",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!ImageAttachHelper.SniffMatchesMime(bytes.AsSpan(0, Math.Min(12, bytes.Length)), mime))
            {
                MessageBox.Show(this, "Содержимое не совпадает с расширением файла.", "Файл",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var fitted = FitOutgoingImage(bytes, mime);
            if (fitted.Bytes == null)
            {
                MessageBox.Show(this, fitted.Error ?? "Не удалось сжать изображение.", "Сжатие",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _media.ValidateMime(fitted.Mime);
            await _p2pSession!.SendImageAsync(fitted.Bytes, fitted.Mime).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send image failed in chat {ChatId}", _chat.Id);
            ShowSendError(ex.Message);
        }
        finally
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    private (byte[]? Bytes, string Mime, string? Error) FitOutgoingImage(byte[] bytes, string mime)
    {
        var limit = MediaEconomy.ImageLimit(_routing);
        if (bytes.Length <= limit)
            return (bytes, mime, null);

        if (!ImageAttachmentCompressor.TryCompressToMaxBytes(bytes, limit, out var compressed, out var err))
            return (null, mime, err ?? "Не удалось уложиться в лимит.");

        return (compressed, ImageAttachmentCompressor.SuggestMimeAfterCompression(), null);
    }

    private async Task OnAttachDocumentAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        EnsureSession(user);
        if (_p2pSession == null)
            return;

        if (BlockBinaryOnMeshtastic("file"))
            return;

        using var dlg = new OpenFileDialog
        {
            Title = "Документ (до 20 МБ) или видео (до 60 МБ)",
            Filter =
                "Документы и видео|*.doc;*.docx;*.rtf;*.pdf;*.odt;*.ods;*.odp;*.odg;*.xlsx;*.xls;*.pptx;*.ppt;*.mp4;*.mov;*.avi;*.wmv;*.webm;*.ogv|Все файлы|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dlg.FileNames)
            await SendPickedDocumentAsync(path).ConfigureAwait(true);
    }

    private async Task SendPickedDocumentAsync(string path)
    {
        try
        {
            var isVideo = TryGetVideoMime(path, out var videoMime);
            if (!isVideo && !DocumentAttachHelper.TryGetMimeFromExtension(path, out videoMime))
            {
                MessageBox.Show(this,
                    "Допустимы офисные документы и видео (.mp4, .mov, .avi, .webm, .ogv, .wmv).",
                    "Файл", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (BlockBinaryOnMeshtastic(isVideo ? "video" : "file"))
                return;

            var mime = videoMime;
            var bytes = await ReadAllBytesAsync(path).ConfigureAwait(true);
            if (bytes.Length == 0)
            {
                MessageBox.Show(this, "Файл пустой.", "Файл",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!isVideo)
            {
                var headLen = Math.Min(4096, bytes.Length);
                if (!DocumentAttachHelper.SniffMatchesMime(bytes.AsSpan(0, headLen), mime))
                {
                    MessageBox.Show(this,
                        "Содержимое не совпадает с типом файла (ожидается корректный Office/LibreOffice/PDF).",
                        "Файл", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            var limit = isVideo ? MediaEconomy.VideoLimit(_routing) : MediaEconomy.DocumentLimit(_routing);
            if (bytes.Length > limit)
            {
                MessageBox.Show(this, FormatByteLimit(limit),
                    "Размер", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _media.ValidateDocumentMime(mime);
            if (isVideo)
                _media.ValidateVideoSize(bytes.Length, _routing.TrafficQuality);
            else
                _media.ValidateDocumentSize(bytes.Length, _routing.TrafficQuality);
            await _p2pSession!.SendFileAsync(path, bytes, mime).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send document failed in chat {ChatId}", _chat.Id);
            ShowSendError(ex.Message);
        }
        finally
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    private void OnAttachVoice()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        EnsureSession(user);
        if (_p2pSession == null)
            return;

        if (_voiceWaveIn == null && BlockBinaryOnMeshtastic("voice", LocalizationUtils.GetStringByKey("chat.voice")))
            return;

        if (_voiceWaveIn != null)
        {
            _voiceDiscardNextStop = false;
            try
            {
                _voiceWaveIn.StopRecording();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stop voice recording");
                CleanupVoiceRecordingHardware();
                _attachVoice.BackColor = SystemColors.Control;
            }

            return;
        }

        try
        {
            _voiceDiscardNextStop = false;
            _voiceWaveMs = new MemoryStream();
            var wf = new WaveFormat(16000, 16, 1);
            _voiceWaveWriter = new WaveFileWriter(_voiceWaveMs, wf);
            _voiceWaveIn = new WaveInEvent
            {
                WaveFormat = wf,
                BufferMilliseconds = 100
            };
            _voiceWaveIn.DataAvailable += VoiceWaveInOnDataAvailable;
            _voiceWaveIn.RecordingStopped += VoiceWaveInOnRecordingStopped;
            _voiceRecordStartUtc = DateTime.UtcNow;
            _voiceWaveIn.StartRecording();
            _attachVoice.BackColor = Color.MistyRose;
            _attachVoice.Text = "■";
            _voiceRecordTimer?.Dispose();
            _voiceRecordTimer = new Timer { Interval = 400 };
            _voiceRecordTimer.Tick += (_, _) =>
            {
                if (_voiceWaveIn == null)
                    return;
                if ((DateTime.UtcNow - _voiceRecordStartUtc).TotalSeconds >= MaxVoiceRecordSeconds)
                {
                    _voiceDiscardNextStop = false;
                    try
                    {
                        _voiceWaveIn.StopRecording();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            };
            _voiceRecordTimer.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voice record start failed");
            CleanupVoiceRecordingHardware();
            ResetVoiceButton();
            MessageBox.Show(this, ex.Message, "Микрофон", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void VoiceWaveInOnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
            return;
        lock (_voiceCapLock)
        {
            try
            {
                _voiceWaveWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            }
            catch
            {
                // disposed
            }
        }
    }

    private void VoiceWaveInOnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        byte[] wav;
        lock (_voiceCapLock)
        {
            try
            {
                _voiceWaveWriter?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceWaveWriter = null;
            try
            {
                wav = _voiceWaveMs?.ToArray() ?? [];
            }
            catch
            {
                wav = [];
            }

            try
            {
                _voiceWaveMs?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceWaveMs = null;
        }

        try
        {
            _voiceWaveIn?.Dispose();
        }
        catch
        {
            // ignore
        }

        _voiceWaveIn = null;

        var discard = _voiceDiscardNextStop;
        _voiceDiscardNextStop = false;

        if (!IsHandleCreated)
            return;

        BeginInvoke(new Action(async () =>
        {
            ResetVoiceButton();
            try
            {
                _voiceRecordTimer?.Stop();
            }
            catch
            {
                // ignore
            }

            try
            {
                _voiceRecordTimer?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceRecordTimer = null;

            if (e.Exception != null)
            {
                _logger.LogWarning(e.Exception, "Voice recording stopped with error");
                MessageBox.Show(this, e.Exception.Message, "Запись", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (discard || wav.Length < 200)
                return;

            UseWaitCursor = true;
            try
            {
                var bitrate = MediaEconomy.SpeechBitrate(_routing);
                var (ok, ogg, err) = await VoiceRecordHelper
                    .EncodeWavPcmToOggOpusAsync(wav, bitrate)
                    .ConfigureAwait(true);
                if (!ok || ogg == null)
                {
                    MessageBox.Show(this, err ?? "Кодирование в Ogg не удалось.", "Голосовое",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var voiceLimit = MediaEconomy.VoiceLimit(_routing);
                if (ogg.Length > voiceLimit)
                {
                    MessageBox.Show(this, FormatByteLimit(voiceLimit), "Размер",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _media.ValidateDocumentMime(VoiceRecordHelper.VoiceMessageMime);
                _media.ValidateVoiceSize(ogg.Length, _routing.TrafficQuality);
                await _p2pSession!
                    .SendFileAsync(VoiceRecordHelper.VoiceFileName, ogg, VoiceRecordHelper.VoiceMessageMime)
                    .ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Send voice failed");
                ShowSendError(ex.Message);
            }
            finally
            {
                UseWaitCursor = false;
                await ReloadAsync().ConfigureAwait(true);
            }
        }));
    }

    private void CleanupVoiceRecordingHardware()
    {
        try
        {
            _voiceRecordTimer?.Stop();
        }
        catch
        {
            // ignore
        }

        try
        {
            _voiceRecordTimer?.Dispose();
        }
        catch
        {
            // ignore
        }

        _voiceRecordTimer = null;
        lock (_voiceCapLock)
        {
            try
            {
                _voiceWaveWriter?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceWaveWriter = null;
            try
            {
                _voiceWaveMs?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceWaveMs = null;
        }

        try
        {
            _voiceWaveIn?.Dispose();
        }
        catch
        {
            // ignore
        }

        _voiceWaveIn = null;
    }

    private void ResetVoiceButton()
    {
        _attachVoice.BackColor = SystemColors.Control;
        _attachVoice.Text = "🎤";
    }

    private static string FormatByteLimit(int bytes)
    {
        if (bytes >= 1024 * 1024 && bytes % (1024 * 1024) == 0)
            return $"В данной версии размер передаваемых файлов ограничен {bytes / (1024 * 1024)} МБ.";
        var kb = (bytes + 1023) / 1024;
        return $"В данной версии размер передаваемых файлов ограничен {kb} КБ.";
    }

    private static bool TryGetVideoMime(string path, out string mime)
    {
        mime = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".wmv" => "video/x-ms-wmv",
            ".webm" => "video/webm",
            ".ogv" => "video/ogg",
            _ => ""
        };
        return mime.Length > 0;
    }

    private static Task<byte[]> ReadAllBytesAsync(string path) =>
        Task.Run(() => File.ReadAllBytes(path));

    /// <summary>TRL-34: UI gate + Info log; protocol layer also blocks in ShortP2P.</summary>
    private bool BlockBinaryOnMeshtastic(string what, string? dialogTitle = null)
    {
        if (_p2pSession?.IsMeshtasticPathActive != true)
            return false;
        _logger.LogInformation("{Prefix} {What} (chat {ChatId})",
            ChatP2PSession.MeshtasticBinarySkipLogPrefix, what, _chat.Id);
        MessageBox.Show(this,
            LocalizationUtils.GetStringByKey("chat.binary_meshtastic_forbidden"),
            dialogTitle ?? LocalizationUtils.GetStringByKey("chat.file"),
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        return true;
    }
}
