using TorgLink.Maui.Helpers;
using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Services;
using ShortP2P.Crypto;
using TorgLink.Localization;

namespace TorgLink.Maui;

public partial class AddChatPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly ILogger<AddChatPage> _logger;
    private readonly UserP2pRuntime _p2p;

    public AddChatPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p, ILogger<AddChatPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        _logger = logger;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedUi();
    }

    private void ApplyLocalizedUi()
    {
        Title = LocalizationUtils.GetStringByKey("addchat.title");
        CancelToolbar.Text = LocalizationUtils.GetStringByKey("cancel");
        NickLabel.Text = LocalizationUtils.GetStringByKey("addchat.nick");
        IdLabel.Text = LocalizationUtils.GetStringByKey("addchat.id");
        PubKeyLabel.Text = LocalizationUtils.GetStringByKey("addchat.pubkey");
        HostLabel.Text = LocalizationUtils.GetStringByKey("addchat.host");
        PortLabel.Text = LocalizationUtils.GetStringByKey("addchat.port");
        PeerNickEntry.Placeholder = LocalizationUtils.GetStringByKey("addchat.ph_nick");
        PeerIdEntry.Placeholder = LocalizationUtils.GetStringByKey("addchat.ph_id");
        PeerPubKeyEditor.Placeholder = LocalizationUtils.GetStringByKey("addchat.ph_key");
        ScanCamButton.Text = LocalizationUtils.GetStringByKey("addchat.scan_cam");
        ScanImgButton.Text = LocalizationUtils.GetStringByKey("addchat.scan_img");
        SaveButton.Text = LocalizationUtils.GetStringByKey("save");
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync().ConfigureAwait(true);
    }

    private async void OnScanQrClicked(object? sender, EventArgs e)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = LocalizationUtils.GetStringByKey("addchat.qr_picker"),
            FileTypes = FilePickerFileType.Images
        }).ConfigureAwait(true);

        if (result == null)
            return;

        await using var stream = await result.OpenReadAsync().ConfigureAwait(true);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms).ConfigureAwait(true);
        var bytes = ms.ToArray();
        AppLog.BinaryLoaded("peer-qr", result.FileName, bytes.Length);

        if (!PeerQrService.TryDecodeImage(bytes, out var payload, out var err))
        {
            _logger.LogWarning("QR decode failed from file: {Error}", err);
            await DisplayAlert(LocalizationUtils.GetStringByKey("qr.title"), err ?? LocalizationUtils.GetStringByKey("addchat.qr_fail"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        ApplyQrPayload(payload);
        await TryInstallChatFromQrAsync(payload).ConfigureAwait(true);
    }

    private async void OnScanQrCameraClicked(object? sender, EventArgs e)
    {
        if (!await EnsureCameraPermissionAsync().ConfigureAwait(true))
            return;

#if ANDROID
        try
        {
            var qrText = await MainActivity.TryScanQrWithSystemScannerAsync().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(qrText))
            {
                if (PeerQrCodec.TryDeserialize(qrText.Trim(), out var payloadFromScanner, out var errFromScanner))
                {
                    ApplyQrPayload(payloadFromScanner);
                    await TryInstallChatFromQrAsync(payloadFromScanner).ConfigureAwait(true);
                    return;
                }

                _logger.LogWarning("System QR scanner returned invalid payload: {Error}", errFromScanner);
                await DisplayAlert(LocalizationUtils.GetStringByKey("qr.title"), errFromScanner ?? LocalizationUtils.GetStringByKey("addchat.qr_bad"), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "System QR scanner invocation failed");
        }
#endif

        FileResult? photo;
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("addchat.cam_unsupported"), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return;
            }

            photo = await MediaPicker.Default.CapturePhotoAsync().ConfigureAwait(true);
        }
        catch (PermissionException ex)
        {
            _logger.LogWarning(ex, "Camera permission denied");
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("addchat.cam_perm"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Camera capture failed");
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKeyWithFormat("addchat.cam_open", ex.Message), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return;
        }

        if (photo == null)
            return;

        await using var stream = await photo.OpenReadAsync().ConfigureAwait(true);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms).ConfigureAwait(true);
        var bytes = CameraHelper.Normalize(ms.ToArray());
        AppLog.BinaryLoaded("peer-qr-camera", photo.FileName, bytes.Length);

        if (!PeerQrService.TryDecodeImage(bytes, out var payload, out var err))
        {
            _logger.LogWarning("QR decode failed from camera photo: {Error}", err);
            await DisplayAlert(LocalizationUtils.GetStringByKey("qr.title"), err ?? LocalizationUtils.GetStringByKey("addchat.cam_photo"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        ApplyQrPayload(payload);
        await TryInstallChatFromQrAsync(payload).ConfigureAwait(true);
    }

    private async Task<bool> EnsureCameraPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>().ConfigureAwait(true);
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Camera>().ConfigureAwait(true);
            if (status == PermissionStatus.Granted)
                return true;
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("addchat.cam_perm"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return false;
        }
        catch (FileNotFoundException ex) when (
            ex.FileName?.Contains("AppxManifest.xml", StringComparison.OrdinalIgnoreCase) == true ||
            ex.Message.Contains("AppxManifest.xml", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Camera permission failed: AppxManifest missing");
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("chat.camera_windows_manifest"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Camera permission request failed");
            await DisplayAlert(LocalizationUtils.GetStringByKey("chat.camera"), LocalizationUtils.GetStringByKey("addchat.cam_perm_req"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return false;
        }
    }

    private void ApplyQrPayload(PeerQrPayload payload)
    {
        PeerNickEntry.Text = payload.N;
        PeerIdEntry.Text = payload.Id;
        PeerPubKeyEditor.Text = payload.K;
        PeerHostEntry.Text = payload.GetCommaSeparatedHosts();
        PeerPortEntry.Text = payload.P.ToString();
    }

    private async Task TryInstallChatFromQrAsync(PeerQrPayload payload)
    {
        var u = _auth.CurrentUser;
        if (u == null)
            return;

        try
        {
            var chat = await _chats
                .AddChatAsync(u.Id, payload.N, payload.Id, payload.K, payload.GetCommaSeparatedHosts(), payload.P,
                    keySource: PeerKeySource.Qr())
                .ConfigureAwait(true);

            await _p2p.TryEnsureChatSessionStartedAsync(chat.Id, SynchronizationContext.Current).ConfigureAwait(true);

            await Navigation.PopModalAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QR auto-install failed");
            await DisplayAlert(LocalizationUtils.GetStringByKey("qr.title"), LocalizationUtils.GetStringByKeyWithFormat("addchat.auto_fail", ex.Message), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var u = _auth.CurrentUser;
        if (u == null)
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("error"), LocalizationUtils.GetStringByKey("addchat.not_logged"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        var nick = PeerNickEntry.Text?.Trim() ?? "";
        var id = PeerIdEntry.Text?.Trim() ?? "";
        var pub = PeerPubKeyEditor.Text?.Trim() ?? "";
        var host = PeerHostEntry.Text?.Trim() ?? "";
        if (!int.TryParse(PeerPortEntry.Text, out var port))
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("error"), LocalizationUtils.GetStringByKey("addchat.bad_port"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        if (nick.Length == 0 || id.Length == 0 || pub.Length == 0 || host.Length == 0)
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("error"), LocalizationUtils.GetStringByKey("addchat.fill_all"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        try
        {
            _ = RsaKeySerializer.DeserializePublic(pub);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid public key when saving chat");
            await DisplayAlert(LocalizationUtils.GetStringByKey("error"), LocalizationUtils.GetStringByKey("addchat.bad_key"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        var chat = await _chats.AddChatAsync(u.Id, nick, id, pub, host, port, keySource: PeerKeySource.Manual())
            .ConfigureAwait(true);
        try
        {
            await _p2p.TryEnsureChatSessionStartedAsync(chat.Id, SynchronizationContext.Current).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Start P2P session after add chat");
        }

        await Navigation.PopModalAsync().ConfigureAwait(true);
    }
}

