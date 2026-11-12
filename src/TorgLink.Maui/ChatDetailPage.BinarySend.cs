using TorgLink.Maui.Helpers;
using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Client;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using TorgLink.Localization;

namespace TorgLink.Maui;

public partial class ChatDetailPage
{
    private void QueueBinarySend(Func<ChatP2PSession, CancellationToken, Task> work)
    {
        var session = _p2pSession;
        if (session == null)
            return;
        var mode = MediaEconomy.Mode(_p2p);
        _ = RunBinarySendAsync(session, mode, work);
    }

    private async Task RunBinarySendAsync(
        ChatP2PSession session,
        TrafficQualityMode mode,
        Func<ChatP2PSession, CancellationToken, Task> work)
    {
        try
        {
            await _p2p.BinarySends.RunAsync(mode, ct => work(session, ct)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (OutboundMessageQueuedException ex)
        {
            _logger.LogInformation(ex, "Binary queued until peer is on LAN");
            ReportBinarySendIssue(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Binary send failed");
            ReportBinarySendIssue(ex.Message);
        }
    }

    private void ReportBinarySendIssue(string message) =>
        MainThread.BeginInvokeOnMainThread(() => ShowDeliveryIssue(message));

    private Task UiAlertAsync(string title, string body) =>
        MainThread.InvokeOnMainThreadAsync(() => DisplayAlert(title, body, LocalizationUtils.GetStringByKey("ok")));

    private static async Task<IReadOnlyList<FileResult>> PickFilesAsync(PickOptions options)
    {
        try
        {
            var many = await FilePicker.Default.PickMultipleAsync(options).ConfigureAwait(true);
            if (many != null)
                return many.Where(p => p != null).Select(p => p!).ToList();
        }
        catch (Exception)
        {
            // Some platforms only support a single pick.
        }

        var one = await FilePicker.Default.PickAsync(options).ConfigureAwait(true);
        return one == null ? [] : [one];
    }

    private async void OnAttachImageClicked(object? sender, EventArgs e)
    {
        var user = _auth.CurrentUser;
        var chat = _chat;
        if (user == null || chat == null || EnsureP2pSessionAttached(user, chat) == null)
            return;
        ClearDeliveryIssue();
        if (await BlockBinaryOnMeshtasticAsync(_p2pSession, "image", "chat.file").ConfigureAwait(true))
            return;

        await SyncTrafficQualityAsync().ConfigureAwait(true);

        IReadOnlyList<FileResult> picks;
        try
        {
            picks = await PickFilesAsync(new PickOptions
            {
                PickerTitle = LocalizationUtils.GetStringByKey("chat.image_picker"),
                FileTypes = FilePickerFileType.Images
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pick image failed");
            ShowDeliveryIssue(ex.Message);
            return;
        }

        foreach (var pick in picks)
            QueueBinarySend((session, ct) => SendPickedImageAsync(session, pick, ct));
    }

    private async Task SendPickedImageAsync(ChatP2PSession session, FileResult pick, CancellationToken ct)
    {
        if (!ImageAttachHelper.TryGetMimeFromExtension(pick.FileName, out var mime))
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.file"), LocalizationUtils.GetStringByKey("chat.only_images")).ConfigureAwait(false);
            return;
        }

        var bytes = await ReadPickBytesAsync(pick, ct).ConfigureAwait(false);
        AppLog.BinaryLoaded("image", pick.FileName, bytes.Length);
        if (bytes.Length < 12)
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.file"), LocalizationUtils.GetStringByKey("chat.file_too_small")).ConfigureAwait(false);
            return;
        }

        if (!ImageAttachHelper.SniffMatchesMime(bytes.AsSpan(0, Math.Min(12, bytes.Length)), mime))
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.file"), LocalizationUtils.GetStringByKey("chat.file_mismatch")).ConfigureAwait(false);
            return;
        }

        var fitted = FitOutgoingImage(bytes, mime);
        if (fitted.Bytes == null)
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.compress"), fitted.Error ?? LocalizationUtils.GetStringByKey("chat.compress_fail"))
                .ConfigureAwait(false);
            return;
        }

        _media.ValidateMime(fitted.Mime);
        await PrepareBinarySendAsync().ConfigureAwait(false);
        await session.SendImageAsync(fitted.Bytes, fitted.Mime, ct).ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
    }

    private async void OnAttachDocumentClicked(object? sender, EventArgs e)
    {
        var user = _auth.CurrentUser;
        var chat = _chat;
        if (user == null || chat == null || EnsureP2pSessionAttached(user, chat) == null)
            return;
        ClearDeliveryIssue();
        if (await BlockBinaryOnMeshtasticAsync(_p2pSession, "file", "chat.file").ConfigureAwait(true))
            return;

        await SyncTrafficQualityAsync().ConfigureAwait(true);

        IReadOnlyList<FileResult> picks;
        try
        {
            picks = await PickFilesAsync(new PickOptions
            {
                PickerTitle = LocalizationUtils.GetStringByKey("chat.doc_picker"),
                FileTypes = OfficeDocFileTypes
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pick document failed");
            ShowDeliveryIssue(ex.Message);
            return;
        }

        foreach (var pick in picks)
            QueueBinarySend((session, ct) => SendPickedDocumentAsync(session, pick, ct));
    }

    private async Task SendPickedDocumentAsync(ChatP2PSession session, FileResult pick, CancellationToken ct)
    {
        if (!TryGetDocumentOrVideoMime(pick.FileName, out var mime))
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.file"), LocalizationUtils.GetStringByKey("chat.only_docs_video")).ConfigureAwait(false);
            return;
        }

        var bytes = await ReadPickBytesAsync(pick, ct).ConfigureAwait(false);
        AppLog.BinaryLoaded("document", pick.FileName, bytes.Length);
        if (bytes.Length == 0)
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.file"), LocalizationUtils.GetStringByKey("chat.file_empty")).ConfigureAwait(false);
            return;
        }

        var sendName = pick.FileName;
        var isVideo = Video144pTranscoder.IsVideoMime(mime);
        if (await BlockBinaryOnMeshtasticAsync(session, isVideo ? "video" : "file", "chat.file")
                .ConfigureAwait(false))
            return;

        if (!isVideo)
        {
            var headLen = Math.Min(4096, bytes.Length);
            if (!DocumentAttachHelper.SniffMatchesMime(bytes.AsSpan(0, headLen), mime))
            {
                await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.file"), LocalizationUtils.GetStringByKey("chat.file_type_mismatch")).ConfigureAwait(false);
                return;
            }
        }

        var prepared = await PrepareOutgoingFileAsync(bytes, sendName, mime, isVideo, ct).ConfigureAwait(false);
        if (prepared == null)
            return;

        _media.ValidateDocumentMime(prepared.Value.Mime);
        await PrepareBinarySendAsync().ConfigureAwait(false);
        await session.SendFileAsync(prepared.Value.FileName, prepared.Value.Bytes, prepared.Value.Mime, ct)
            .ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
    }

    private async void OnAttachCameraClicked(object? sender, EventArgs e)
    {
        var user = _auth.CurrentUser;
        var chat = _chat;
        if (user == null || chat == null || EnsureP2pSessionAttached(user, chat) == null)
            return;
        ClearDeliveryIssue();
        if (await BlockBinaryOnMeshtasticAsync(_p2pSession, "camera media", "chat.camera").ConfigureAwait(true))
            return;

        var photoLabel = LocalizationUtils.GetStringByKey("preview.photo");
        var videoLabel = LocalizationUtils.GetStringByKey("chat.video");
        var choice = await DisplayActionSheet(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("cancel"), null, photoLabel, videoLabel)
            .ConfigureAwait(true);
        if (string.IsNullOrEmpty(choice) || choice == LocalizationUtils.GetStringByKey("cancel"))
            return;

        if (choice == photoLabel)
            await CaptureAndQueueCameraPhotoAsync().ConfigureAwait(true);
        else if (choice == videoLabel)
        {
            if (await BlockBinaryOnMeshtasticAsync(_p2pSession, "video", "chat.video").ConfigureAwait(true))
                return;

            await CaptureAndQueueCameraVideoAsync().ConfigureAwait(true);
        }
    }

    private async Task CaptureAndQueueCameraPhotoAsync()
    {
        try
        {
            if (await BlockBinaryOnMeshtasticAsync(_p2pSession, "image", "chat.camera").ConfigureAwait(true))
                return;

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_unsupported"), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return;
            }

            var cam = await Permissions.RequestAsync<Permissions.Camera>().ConfigureAwait(true);
            if (cam != PermissionStatus.Granted)
            {
                await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_perm"), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return;
            }

#if ANDROID
            await EnsureLegacyStorageWriteAsync().ConfigureAwait(true);
#endif

            var photo = await MediaPicker.Default.CapturePhotoAsync().ConfigureAwait(true);
            if (photo == null)
                return;

            await SyncTrafficQualityAsync().ConfigureAwait(true);
            QueueBinarySend((session, ct) => SendCameraPhotoAsync(session, photo, ct));
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_unsupported"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
        }
        catch (PermissionException)
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_perm"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
        }
        catch (FileNotFoundException ex) when (IsAppxManifestMissing(ex))
        {
            _logger.LogWarning(ex, "Camera photo failed: AppxManifest missing");
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_windows_manifest"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Capture camera photo failed");
            ShowDeliveryIssue(ex.Message);
        }
    }

    private async Task SendCameraPhotoAsync(ChatP2PSession session, FileResult photo, CancellationToken ct)
    {
        var bytes = await ReadPickBytesAsync(photo, ct).ConfigureAwait(false);
        var fileName = string.IsNullOrWhiteSpace(photo.FileName)
            ? $"camera-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jpg"
            : photo.FileName;
        AppLog.BinaryLoaded("camera-photo", fileName, bytes.Length);
        if (bytes.Length < 12)
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_photo_fail")).ConfigureAwait(false);
            return;
        }

        if (!ImageAttachHelper.TryGetMimeFromExtension(fileName, out var mime))
            mime = "image/jpeg";

        // TRL-31: the shot must be sent as it was displayed. Apply the orientation
        // (rotation / selfie-preview mirror) to the pixels so the photo reads left to right
        // on any platform, including clients that ignore orientation metadata.
        var normalized = CameraHelper.Normalize(bytes);
        if (!ReferenceEquals(normalized, bytes))
        {
            bytes = normalized;
            mime = "image/jpeg";
        }

        var fitted = FitOutgoingImage(bytes, mime);
        if (fitted.Bytes == null)
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.compress"), fitted.Error ?? LocalizationUtils.GetStringByKey("chat.compress_fail"))
                .ConfigureAwait(false);
            return;
        }

        _media.ValidateMime(fitted.Mime);
        await PrepareBinarySendAsync().ConfigureAwait(false);
        await session.SendImageAsync(fitted.Bytes, fitted.Mime, ct).ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
    }

    private async Task CaptureAndQueueCameraVideoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_unsupported"), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return;
            }

            var cam = await Permissions.RequestAsync<Permissions.Camera>().ConfigureAwait(true);
            if (cam != PermissionStatus.Granted)
            {
                await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_perm"), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return;
            }

            var mic = await Permissions.RequestAsync<Permissions.Microphone>().ConfigureAwait(true);
            if (mic != PermissionStatus.Granted)
            {
                await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_mic_perm"), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return;
            }

#if ANDROID
            await EnsureLegacyStorageWriteAsync().ConfigureAwait(true);
#endif

            var video = await MediaPicker.Default.CaptureVideoAsync().ConfigureAwait(true);
            if (video == null)
                return;

            await SyncTrafficQualityAsync().ConfigureAwait(true);
            QueueBinarySend((session, ct) => SendCameraVideoAsync(session, video, ct));
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_unsupported"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
        }
        catch (PermissionException)
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_perm"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
        }
        catch (FileNotFoundException ex) when (IsAppxManifestMissing(ex))
        {
            _logger.LogWarning(ex, "Camera video failed: AppxManifest missing");
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_windows_manifest"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Capture camera video failed");
            ShowDeliveryIssue(ex.Message);
        }
    }

    private async Task SendCameraVideoAsync(ChatP2PSession session, FileResult video, CancellationToken ct)
    {
        if (await BlockBinaryOnMeshtasticAsync(session, "video", "chat.video").ConfigureAwait(false))
            return;

        var bytes = await ReadPickBytesAsync(video, ct).ConfigureAwait(false);
        var fileName = string.IsNullOrWhiteSpace(video.FileName)
            ? $"camera-{DateTime.UtcNow:yyyyMMdd-HHmmss}.mp4"
            : video.FileName;
        AppLog.BinaryLoaded("camera-video", fileName, bytes.Length);
        if (bytes.Length < 32)
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_fail")).ConfigureAwait(false);
            return;
        }

        if (!TryGetDocumentOrVideoMime(fileName, out var mime))
            mime = "video/mp4";

        var prepared = await PrepareOutgoingFileAsync(bytes, fileName, mime, isVideo: true, ct).ConfigureAwait(false);
        if (prepared == null)
            return;

        _media.ValidateDocumentMime(prepared.Value.Mime);
        await PrepareBinarySendAsync().ConfigureAwait(false);
        await session.SendFileAsync(prepared.Value.FileName, prepared.Value.Bytes, prepared.Value.Mime, ct)
            .ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
    }

    private async Task FinishAndSendVoiceAsync(
        ChatP2PSession session, VoiceRecordingSession voice, CancellationToken ct)
    {
        try
        {
            if (await BlockBinaryOnMeshtasticAsync(session, "voice", "chat.voice").ConfigureAwait(false))
                return;

            var recorded = await voice.TakeResultAsync(ct).ConfigureAwait(false);
            AppLog.BinaryLoaded("voice", recorded.FileName, recorded.Bytes.Length);
            if (recorded.Bytes.Length > MediaEconomy.VoiceLimit(_p2p))
            {
                await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.size"), FormatByteLimit(MediaEconomy.VoiceLimit(_p2p)))
                    .ConfigureAwait(false);
                return;
            }

            _media.ValidateDocumentMime(recorded.MimeType);
            _media.ValidateVoiceSize(recorded.Bytes.Length, MediaEconomy.Mode(_p2p));
            await PrepareBinarySendAsync().ConfigureAwait(false);
            await session.SendFileAsync(recorded.FileName, recorded.Bytes, recorded.MimeType, ct)
                .ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
        }
        finally
        {
            try
            {
                await voice.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static async Task<byte[]> ReadPickBytesAsync(FileResult pick, CancellationToken ct)
    {
        await using var stream = await pick.OpenReadAsync().ConfigureAwait(false);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    private (byte[]? Bytes, string Mime, string? Error) FitOutgoingImage(byte[] bytes, string mime)
    {
        var limit = MediaEconomy.ImageLimit(_p2p);
        if (bytes.Length <= limit)
            return (bytes, mime, null);

        if (!ImageAttachmentCompressor.TryCompressToMaxBytes(bytes, limit, out var compressed, out var err))
            return (null, mime, err ?? LocalizationUtils.GetStringByKey("chat.compress_fail"));

        return (compressed, ImageAttachmentCompressor.SuggestMimeAfterCompression(), null);
    }

    private async Task<(byte[] Bytes, string Mime, string FileName)?> PrepareOutgoingFileAsync(
        byte[] bytes, string fileName, string mime, bool isVideo, CancellationToken ct)
    {
        if (isVideo && MediaEconomy.UsesReducedMedia(_p2p))
        {
            var ext = Path.GetExtension(fileName);
            var temp = Path.Combine(FileSystem.CacheDirectory,
                $"torglink_bin_{DateTime.UtcNow.Ticks}{(string.IsNullOrEmpty(ext) ? ".mp4" : ext)}");
            await File.WriteAllBytesAsync(temp, bytes, ct).ConfigureAwait(false);
            try
            {
                var prepared = await Video144pTranscoder
                    .PrepareAsync(temp, fileName, mime, MediaEconomy.Mode(_p2p))
                    .ConfigureAwait(false);
                if (!prepared.Ok || prepared.Bytes == null)
                {
                    await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.video"),
                            prepared.Error ?? LocalizationUtils.GetStringByKeyWithFormat("chat.video_transcode_fail",
                                MediaEconomy.VideoResolutionLabel(_p2p)))
                        .ConfigureAwait(false);
                    return null;
                }

                bytes = prepared.Bytes;
                mime = prepared.Mime;
                fileName = prepared.FileName;
                AppLog.BinaryLoaded("video-economy", fileName, bytes.Length);
            }
            finally
            {
                try
                {
                    File.Delete(temp);
                }
                catch
                {
                    // ignore
                }
            }
        }
        else if (bytes.Length > OutgoingFileLimit(isVideo))
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.size"), FormatByteLimit(OutgoingFileLimit(isVideo))).ConfigureAwait(false);
            return null;
        }

        if (bytes.Length > OutgoingFileLimit(isVideo))
        {
            await UiAlertAsync(LocalizationUtils.GetStringByKey("chat.size"), LocalizationUtils.GetStringByKey("chat.size_still")).ConfigureAwait(false);
            return null;
        }

        return (bytes, mime, fileName);
    }

    private int OutgoingFileLimit(bool isVideo) =>
        isVideo ? MediaEconomy.VideoLimit(_p2p) : MediaEconomy.DocumentLimit(_p2p);

    private static string FormatByteLimit(int bytes)
    {
        if (bytes >= 1024 * 1024 && bytes % (1024 * 1024) == 0)
            return $"В данной версии размер передаваемых файлов ограничен {bytes / (1024 * 1024)} МБ";
        var kb = (bytes + 1023) / 1024;
        return $"В данной версии размер передаваемых файлов ограничен {kb} кБ";
    }

    private async Task PrepareBinarySendAsync()
    {
        await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(false);
    }

    /// <summary>TRL-34: UI gate + Info log; protocol layer also blocks in ShortP2P.</summary>
    private async Task<bool> BlockBinaryOnMeshtasticAsync(
        ChatP2PSession? session, string what, string titleKey)
    {
        if (session?.IsMeshtasticPathActive != true)
            return false;
        var chatId = _chat?.Id ?? 0;
        _logger.LogInformation("{Prefix} {What} (chat {ChatId})",
            ChatP2PSession.MeshtasticBinarySkipLogPrefix, what, chatId);
        await UiAlertAsync(
                LocalizationUtils.GetStringByKey(titleKey),
                LocalizationUtils.GetStringByKey("chat.binary_meshtastic_forbidden"))
            .ConfigureAwait(false);
        return true;
    }
}

