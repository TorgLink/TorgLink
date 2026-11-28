using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using TorgLink.Localization;

namespace TorgLink.WinForms;

public sealed partial class AddChatForm : AppForm
{
    private readonly AuthService _auth = null!;
    private readonly ChatRepository _chats = null!;
    private readonly MessengerServerSyncService _sync = null!;
    private readonly ILogger<AddChatForm> _logger = null!;

    public ChatEntity? CreatedChat { get; private set; }

    public AddChatForm()
    {
        InitializeComponent();
    }

    public AddChatForm(
        AuthService auth,
        ChatRepository chats,
        MessengerServerSyncService sync,
        ILogger<AddChatForm> logger)
        : this()
    {
        _auth = auth;
        _chats = chats;
        _sync = sync;
        _logger = logger;

        _qrFile.Click += OnQrFromFile;
        _save.Click += async (_, _) => await OnSaveAsync().ConfigureAwait(true);
        ApplyLocalizedUi();
    }

    protected override void ApplyLocalizedUi()
    {
        Text = LocalizationUtils.GetStringByKey("addchat.title");
        _lblNick.Text = LocalizationUtils.GetStringByKey("addchat.nick");
        _lblId.Text = LocalizationUtils.GetStringByKey("addchat.id");
        _lblPub.Text = LocalizationUtils.GetStringByKey("addchat.pubkey");
        _lblHost.Text = LocalizationUtils.GetStringByKey("addchat.host");
        _qrFile.Text = LocalizationUtils.GetStringByKey("addchat.scan_img");
        _save.Text = LocalizationUtils.GetStringByKey("save");
        _cancel.Text = LocalizationUtils.GetStringByKey("cancel");
    }

    private void OnQrFromFile(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "QR-код пира",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            var bytes = File.ReadAllBytes(dlg.FileName);
            if (!PeerQrService.TryDecodeImage(bytes, out var payload, out var err) || payload == null)
            {
                MessageBox.Show(this, err ?? "QR не распознан.", "QR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _nick.Text = payload.N;
            _id.Text = payload.Id;
            _pub.Text = payload.K;
            _host.Text = payload.GetCommaSeparatedHosts();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QR from file");
            MessageBox.Show(this, ex.Message, "QR", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OnSaveAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        var id = _id.Text.Trim();
        var pub = _pub.Text.Trim();
        if (id.Length == 0 || pub.Length == 0)
        {
            MessageBox.Show(this, "Нужны network id и публичный ключ.", "Добавить чат", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var chat = await _chats.AddChatAsync(
                user.Id,
                _nick.Text.Trim(),
                id,
                pub,
                string.IsNullOrWhiteSpace(_host.Text) ? id : _host.Text.Trim(),
                user.DataUdpPort,
                remote: false,
                keySource: PeerKeySource.Qr()).ConfigureAwait(true);
            await _sync.PublishChatRequestAsync(chat.PeerNetworkIdShort).ConfigureAwait(true);
            CreatedChat = chat;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Add chat");
            MessageBox.Show(this, ex.Message, "Добавить чат", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
