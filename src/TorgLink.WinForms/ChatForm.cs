using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Discovery;
using TorgLink.Localization;

namespace TorgLink.WinForms;

public sealed partial class ChatForm : AppForm
{
    private readonly AuthService _auth = null!;
    private readonly ChatRepository _chats = null!;
    private readonly ChatSessionCache _sessions = null!;
    private readonly MessengerServerSyncService _sync = null!;
    private readonly MessengerServerManager _messengerServers = null!;
    private readonly PeerBlacklist _blacklist = null!;
    private readonly ChatMediaOptions _media = null!;
    private readonly P2pRoutingSettings _routing = null!;
    private ChatEntity _chat = null!;
    private readonly ILogger _logger = null!;
    private static readonly Color PeerMessageColor = Color.FromArgb(0x00, 0x99, 0x99);
    private static readonly Color PreviewColor = Color.FromArgb(0x66, 0x66, 0x66);
    private readonly ConcurrentDictionary<int, byte> _binaryDownloadsInFlight = new();
    private readonly ToolTip _buttonTooltips = new() { ShowAlways = true };

    private ChatP2PSession? _p2pSession;
    private List<SidebarRow> _sidebarItems = [];
    private readonly HashSet<int> _unreadChatIds = [];
    private bool _sidebarSelectSuppressed;
    private bool _switchBusy;
    private bool _splitterInitialized;
    private int _sidebarPreferredWidth = 180;
    private int _messagesLayoutWidth;
    private bool _messagesLayoutReady;
    private bool _relayoutingMessages;
    private const int SidebarMinWidth = 140;
    private const int MessagesMinWidth = 240;
    private const int MessageTextPadX = 2;
    private const int MessageRowPad = 8;
    private const TextFormatFlags MessageWrapFlags =
        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;

    public int ActiveChatId => _chat.Id;

    /// <summary>Raised after the form successfully switches to another chat (sidebar or <see cref="SwitchToChatAsync"/>).</summary>
    public event EventHandler<ChatSwitchedEventArgs>? ActiveChatChanged;

    /// <summary>
    /// Raised when the user picks another chat in the sidebar. Set <see cref="ChatSwitchRequestEventArgs.Handled"/>
    /// to true to cancel the in-window switch (e.g. another ChatForm already shows that chat).
    /// </summary>
    public event EventHandler<ChatSwitchRequestEventArgs>? ChatSwitchRequested;

    public ChatForm()
    {
        InitializeComponent();
        ApplyListRowMetrics();
    }

    public ChatForm(
        AuthService auth,
        ChatRepository chats,
        ChatSessionCache sessions,
        MessengerServerSyncService sync,
        MessengerServerManager messengerServers,
        PeerBlacklist blacklist,
        ChatMediaOptions media,
        P2pRoutingSettings routing,
        ChatEntity chat,
        ILogger logger)
        : this()
    {
        _auth = auth;
        _chats = chats;
        _sessions = sessions;
        _sync = sync;
        _messengerServers = messengerServers;
        _blacklist = blacklist;
        _media = media;
        _routing = routing;
        _chat = chat;
        _logger = logger;
        Text = FormatTitle(chat);

        _send.Click += async (_, _) => await SendAsync().ConfigureAwait(true);
        _attachVoice.Click += (_, _) => OnAttachVoice();
        _attachImage.Click += async (_, _) => await OnAttachImageAsync().ConfigureAwait(true);
        _attachDocument.Click += async (_, _) => await OnAttachDocumentAsync().ConfigureAwait(true);
        _messages.DrawItem += OnMessagesDrawItem;
        _messages.MeasureItem += OnMessagesMeasureItem;
        _messages.Resize += (_, _) => RelayoutMessageHeights();
        _messages.MouseClick += OnMessagesMouseClick;

        ApplyLocalizedUi();
        RefreshBotCommandsButton();
        _deliveryPath.Click += (_, _) => ShowDeliveryPathMenu();
        _botCommands.Click += async (_, _) => await ShowBotCommandsAsync().ConfigureAwait(true);
        _blockPeer.Click += async (_, _) => await BlockPeerAsync().ConfigureAwait(true);

        _sidebar.DrawItem += OnSidebarDrawItem;
        _sidebar.SelectedIndexChanged += OnSidebarSelectedIndexChanged;

        Load += (_, _) => ApplySplitterLayout();
        Shown += (_, _) =>
        {
            ApplySplitterLayout();
            _messagesLayoutReady = true;
            RelayoutMessageHeights();
        };
        Resize += (_, _) => ApplySplitterLayout();
        Load += async (_, _) => await OnLoadAsync().ConfigureAwait(true);
        _chats.ChatMessageAppended += OnRepoMessagesChanged;
        _chats.ChatMessageDeliveryChanged += OnRepoMessagesChanged;
        _chats.ChatListChanged += OnChatListChanged;
        FormClosed += (_, _) =>
        {
            _voiceDiscardNextStop = true;
            CleanupVoiceRecordingHardware();

            if (_p2pSession != null)
            {
                _p2pSession.MessagesChanged -= OnP2pMessagesChanged;
                _p2pSession.DeliveryPathChanged -= OnDeliveryPathChanged;
                _p2pSession = null;
            }

            _chats.ChatMessageAppended -= OnRepoMessagesChanged;
            _chats.ChatMessageDeliveryChanged -= OnRepoMessagesChanged;
            _chats.ChatListChanged -= OnChatListChanged;
            _buttonTooltips.Dispose();
        };
    }

    /// <summary>
    /// Item heights and sidebar width after the form 12pt font is applied (not the default 8.25pt).
    /// Two-line sidebar rows: bold name + preview. Width fits a short nick (e.g. Ragnarnar).
    /// </summary>
    private void ApplyListRowMetrics()
    {
        using var bold = new Font(Font, FontStyle.Bold);
        _sidebar.ItemHeight = 4 + bold.Height + Font.Height + 4;
        _messages.ItemHeight = Math.Max(Font.Height + 8, 26);
        var nickWidth = TextRenderer.MeasureText("Ragnarnar", bold).Width;
        _sidebarPreferredWidth = Math.Max(180, nickWidth + 28);

        // 3 visible lines + single-line TextBox chrome (PreferredHeight is always 1-line).
        var chrome = Math.Max(8, _input.PreferredHeight - _input.Font.Height);
        _bottom.Height = _input.Font.Height * 3 + chrome + _bottom.Padding.Vertical;
    }

    /// <summary>Switch the open conversation in this window (reload DB messages + rebind session cache).</summary>
    public async Task SwitchToChatAsync(ChatEntity chat)
    {
        if (IsDisposed || chat.Id == _chat.Id)
            return;

        if (InvokeRequired)
        {
            await (Task)Invoke(new Func<Task>(() => SwitchToChatAsync(chat)));
            return;
        }

        if (_switchBusy)
            return;

        _switchBusy = true;
        try
        {
            var oldId = _chat.Id;
            DetachP2pMessagesChanged();

            _chat = chat;
            _unreadChatIds.Remove(chat.Id);
            Text = FormatTitle(chat);
            RefreshBotCommandsButton();
            SelectSidebarChat(chat.Id);

            var user = _auth.CurrentUser;
            if (user != null)
                EnsureSession(user);

            await ReloadAsync().ConfigureAwait(true);

            if (_p2pSession != null && !_sessions.IsStarted(_chat.Id))
            {
                try
                {
                    await _p2pSession.StartAsync().ConfigureAwait(true);
                    _sessions.MarkStarted(_chat.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Chat session start for chat {ChatId}", _chat.Id);
                }
            }

            await RefreshChatEntityAsync().ConfigureAwait(true);
            await RefreshSidebarAsync().ConfigureAwait(true);

            ActiveChatChanged?.Invoke(this, new ChatSwitchedEventArgs(oldId, _chat.Id));
        }
        finally
        {
            _switchBusy = false;
        }
    }

    private void ApplySplitterLayout()
    {
        if (_split.IsDisposed || !_split.IsHandleCreated)
            return;

        var width = _split.ClientSize.Width;
        var splitterWidth = Math.Max(_split.SplitterWidth, 1);
        if (width <= splitterWidth + 2)
            return;

        // Keep mins within the current width so SplitterDistance never throws on shrink.
        var panel1Min = Math.Min(SidebarMinWidth, Math.Max(0, (width - splitterWidth) / 3));
        var panel2Min = Math.Min(MessagesMinWidth, Math.Max(0, (width - splitterWidth) / 2));
        if (panel1Min + panel2Min + splitterWidth > width)
        {
            panel1Min = 0;
            panel2Min = 0;
        }

        try
        {
            _split.Panel1MinSize = panel1Min;
            _split.Panel2MinSize = panel2Min;

            var maxDistance = width - panel2Min - splitterWidth;
            if (maxDistance < panel1Min)
                return;

            var desired = _splitterInitialized
                ? _split.SplitterDistance
                : _sidebarPreferredWidth;
            var clamped = Math.Max(panel1Min, Math.Min(desired, maxDistance));
            if (_split.SplitterDistance != clamped)
                _split.SplitterDistance = clamped;
            _splitterInitialized = true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Layout not ready yet (common during first show / DPI scale).
        }
    }

    private async Task OnLoadAsync()
    {
        ApplySplitterLayout();

        var user = _auth.CurrentUser;
        if (user != null)
            EnsureSession(user);

        await ReloadAsync().ConfigureAwait(true);
        await RefreshSidebarAsync().ConfigureAwait(true);

        if (_p2pSession == null)
            return;

        try
        {
            if (!_sessions.IsStarted(_chat.Id))
            {
                await _p2pSession.StartAsync().ConfigureAwait(true);
                _sessions.MarkStarted(_chat.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Chat session start for chat {ChatId}", _chat.Id);
        }

        await RefreshChatEntityAsync().ConfigureAwait(true);
        RefreshDeliveryPathButton();
    }

    private void EnsureSession(UserEntity user)
    {
        var uiSync = SynchronizationContext.Current;
        var logger = _logger;
        var sync = _sync;
        DetachP2pMessagesChanged();
        _p2pSession = _sessions.GetSession(
            _chat.Id,
            () => new ChatP2PSession(_chat, user, _chats, sync, uiSync, logger),
            s => s.ApplyChatRow(_chat));
        _p2pSession.MessagesChanged += OnP2pMessagesChanged;
        _p2pSession.DeliveryPathChanged -= OnDeliveryPathChanged;
        _p2pSession.DeliveryPathChanged += OnDeliveryPathChanged;
        RefreshDeliveryPathButton();
    }

    private void DetachP2pMessagesChanged()
    {
        if (_p2pSession == null)
            return;
        _p2pSession.MessagesChanged -= OnP2pMessagesChanged;
        _p2pSession.DeliveryPathChanged -= OnDeliveryPathChanged;
    }

    protected override void ApplyLocalizedUi()
    {
        _send.Text = LocalizationUtils.GetStringByKey("chat.send");
        _buttonTooltips.SetToolTip(_attachVoice, LocalizationUtils.GetStringByKey("chat.voice"));
        _buttonTooltips.SetToolTip(_attachImage, LocalizationUtils.GetStringByKey("chat.image_picker"));
        _buttonTooltips.SetToolTip(_attachDocument, LocalizationUtils.GetStringByKey("chat.doc_picker"));
        _buttonTooltips.SetToolTip(_deliveryPath, LocalizationUtils.GetStringByKey("chat.path_hint"));
        _buttonTooltips.SetToolTip(_send, LocalizationUtils.GetStringByKey("chat.send"));
        _buttonTooltips.SetToolTip(_blockPeer, LocalizationUtils.GetStringByKey("blacklist.add_hint"));
        _botCommands.Text = LocalizationUtils.GetStringByKey("chat.commands");
        _buttonTooltips.SetToolTip(_botCommands, LocalizationUtils.GetStringByKey("chat.commands"));
        RefreshBotCommandsButton();
        RefreshDeliveryPathButton();
        Text = FormatTitle(_chat);
    }

    private async Task BlockPeerAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;

        var id = ChatRepository.CanonicalPeerNetworkId(_chat.PeerNetworkIdShort);
        if (id.Length == 0)
            return;

        await _blacklist.EnsureLoadedAsync(user.Id).ConfigureAwait(true);
        if (_blacklist.IsBlocked(user.Id, id))
        {
            Close();
            return;
        }

        var label = string.IsNullOrWhiteSpace(_chat.PeerNickname) ? id : _chat.PeerNickname.Trim();
        var bodyKey = _chat.IsBot ? "blacklist.add_body_bot" : "blacklist.add_body";
        var confirm = MessageBox.Show(
            this,
            LocalizationUtils.GetStringByKeyWithFormat(bodyKey, label),
            LocalizationUtils.GetStringByKey("blacklist.add_title"),
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.OK)
            return;

        await _blacklist.AddAsync(user.Id, id, label).ConfigureAwait(true);
        if (_chat.IsBot)
        {
            try
            {
                await BotHostClaims.ClaimServersHostingBotAsync(_messengerServers, id).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bot host claim failed for {BotId}", id);
            }
        }

        Close();
    }

    private void RefreshBotCommandsButton()
    {
        _botCommands.Visible = _chat.IsBot;
    }

    private async Task ShowBotCommandsAsync()
    {
        if (!_chat.IsBot)
            return;

        try
        {
            var commands = await _sync.FetchBotCommandsAsync(_chat.PeerNetworkIdShort).ConfigureAwait(true);
            var body = commands.Count == 0
                ? LocalizationUtils.GetStringByKey("chat.commands_empty")
                : BotCommandText.FormatList(commands);
            MessageBox.Show(
                this,
                body,
                LocalizationUtils.GetStringByKey("chat.commands_title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bot commands failed for chat {ChatId}", _chat.Id);
            MessageBox.Show(
                this,
                LocalizationUtils.GetStringByKey("chat.commands_fail"),
                LocalizationUtils.GetStringByKey("chat.commands_title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void RefreshDeliveryPathButton()
    {
        if (_p2pSession == null)
        {
            _deliveryPath.Text = LocalizationUtils.GetStringByKey("chat.path_auto");
            return;
        }

        _deliveryPath.Text = _p2pSession.DeliveryPath switch
        {
            ChatDeliveryPath.Server => LocalizationUtils.GetStringByKey("chat.path_server"),
            ChatDeliveryPath.Mesh => LocalizationUtils.GetStringByKey("chat.path_mesh"),
            _ => LocalizationUtils.GetStringByKey("chat.path_auto")
        };
    }

    private void OnDeliveryPathChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated)
            BeginInvoke(RefreshDeliveryPathButton);
    }

    private void ShowDeliveryPathMenu()
    {
        if (_p2pSession == null)
            return;

        var menu = new ContextMenuStrip();
        ChatDeliveryPath? chosen = null;
        menu.Items.Add(LocalizationUtils.GetStringByKey("chat.path_server_choice"), null,
            (_, _) => chosen = ChatDeliveryPath.Server);
        menu.Items.Add(LocalizationUtils.GetStringByKey("chat.path_mesh_choice"), null,
            (_, _) => chosen = ChatDeliveryPath.Mesh);
        menu.Closed += (_, _) =>
        {
            // Dispose after Closed finishes — disposing inside Closed races ToolStripDropDown.CreateHandle.
            var path = chosen;
            void Finish()
            {
                menu.Dispose();
                if (path != null)
                    _ = ApplyDeliveryPathAsync(path.Value);
            }

            if (IsHandleCreated)
                BeginInvoke(Finish);
            else
                Finish();
        };
        menu.Show(_deliveryPath, new Point(0, _deliveryPath.Height));
    }

    private async Task ApplyDeliveryPathAsync(ChatDeliveryPath path)
    {
        if (_p2pSession == null)
            return;

        _logger.LogInformation("Chat {Peer}: switch delivery path to {Path}", _chat.PeerNickname, path);
        try
        {
            await _p2pSession.SwitchDeliveryPathAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Delivery path switch failed for chat {ChatId}", _chat.Id);
            MessageBox.Show(this, ex.Message, "Путь доставки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        RefreshDeliveryPathButton();
    }

    private void OnChatListChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated)
            BeginInvoke(new Action(() =>
            {
                _ = RefreshChatEntityAsync();
                _ = RefreshSidebarAsync();
            }));
    }

    private void OnRepoMessagesChanged(object? sender, ChatMessageAppendedEventArgs e)
    {
        if (e.ChatId == _chat.Id)
        {
            ScheduleReload();
            ScheduleSidebarRefresh();
            return;
        }

        if (!e.Outgoing)
            MarkSidebarUnread(e.ChatId);
        ScheduleSidebarRefresh();
    }

    private void OnP2pMessagesChanged(object? sender, EventArgs e) => ScheduleReload();

    private void MarkSidebarUnread(int chatId)
    {
        if (IsDisposed)
            return;
        void Mark()
        {
            if (chatId == _chat.Id)
                return;
            _unreadChatIds.Add(chatId);
            _sidebar.Invalidate();
        }

        if (IsHandleCreated && InvokeRequired)
            BeginInvoke(Mark);
        else if (IsHandleCreated)
            Mark();
    }

    private void ScheduleReload()
    {
        if (IsHandleCreated)
            BeginInvoke(new Action(() => _ = ReloadAsync()));
    }

    private void ScheduleSidebarRefresh()
    {
        if (IsHandleCreated)
            BeginInvoke(new Action(() => _ = RefreshSidebarAsync()));
    }

    private async Task RefreshChatEntityAsync()
    {
        var fresh = await _chats.GetChatAsync(_chat.Id).ConfigureAwait(false);
        if (fresh == null)
            return;
        _chat = fresh;
        _p2pSession?.ApplyChatRow(fresh);
        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke(new Action(() =>
            {
                Text = FormatTitle(_chat);
                RefreshBotCommandsButton();
                RefreshDeliveryPathButton();
            }));
            return;
        }

        Text = FormatTitle(_chat);
        RefreshBotCommandsButton();
        RefreshDeliveryPathButton();
    }

    private async Task RefreshSidebarAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;

        try
        {
            var list = await _chats.ListChatsAsync(user.Id).ConfigureAwait(false);
            var rows = new List<SidebarRow>(list.Count);
            foreach (var c in list)
            {
                var lastPage = await _chats
                    .ListMessagesPageDescAsync(c.Id, 0, 1, includePayloadBlob: false)
                    .ConfigureAwait(false);
                var last = lastPage.Count > 0 ? lastPage[0] : null;
                rows.Add(new SidebarRow(c, FormatPreview(last)));
            }

            void Bind()
            {
                if (IsDisposed)
                    return;
                _sidebarItems = rows;
                _sidebarSelectSuppressed = true;
                try
                {
                    _sidebar.BeginUpdate();
                    try
                    {
                        _sidebar.Items.Clear();
                        foreach (var row in _sidebarItems)
                            _sidebar.Items.Add(row);
                    }
                    finally
                    {
                        _sidebar.EndUpdate();
                    }

                    SelectSidebarChat(_chat.Id);
                }
                finally
                {
                    _sidebarSelectSuppressed = false;
                }
            }

            if (IsHandleCreated && InvokeRequired)
                BeginInvoke(new Action(Bind));
            else
                Bind();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refresh chat sidebar");
        }
    }

    private void SelectSidebarChat(int chatId)
    {
        var idx = _sidebarItems.FindIndex(r => r.Chat.Id == chatId);
        _sidebarSelectSuppressed = true;
        try
        {
            _sidebar.SelectedIndex = idx;
        }
        finally
        {
            _sidebarSelectSuppressed = false;
        }

        _sidebar.Invalidate();
    }

    private async void OnSidebarSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_sidebarSelectSuppressed || _switchBusy)
            return;
        var i = _sidebar.SelectedIndex;
        if (i < 0 || i >= _sidebarItems.Count)
            return;

        var target = _sidebarItems[i].Chat;
        if (target.Id == _chat.Id)
        {
            _unreadChatIds.Remove(target.Id);
            _sidebar.Invalidate();
            return;
        }

        var request = new ChatSwitchRequestEventArgs(target);
        ChatSwitchRequested?.Invoke(this, request);
        if (request.Handled)
        {
            SelectSidebarChat(_chat.Id);
            return;
        }

        var fresh = await _chats.GetChatAsync(target.Id).ConfigureAwait(true);
        if (fresh == null || IsDisposed)
        {
            SelectSidebarChat(_chat.Id);
            return;
        }

        await SwitchToChatAsync(fresh).ConfigureAwait(true);
    }

    private async Task ReloadAsync()
    {
        var rows = await _chats.ListMessagesAsync(_chat.Id).ConfigureAwait(false);
        void Bind()
        {
            _messages.Items.Clear();
            foreach (var m in rows)
                _messages.Items.Add(FormatMessageLine(m));

            if (_messages.Items.Count > 0)
                _messages.SelectedIndex = _messages.Items.Count - 1;
        }

        if (IsHandleCreated && InvokeRequired)
            BeginInvoke(new Action(Bind));
        else
            Bind();
    }

    private ChatLine FormatMessageLine(ChatMessageEntity m)
    {
        var who = m.Outgoing ? "я" : _chat.PeerNickname;
        var status = m.Outgoing ? FormatDeliverySuffix(m.DeliveryStatus) : "";
        if (IsAttachmentMessage(m))
        {
            var kind = AttachmentKindLabel(m);
            var name = AttachmentDisplayName(m);
            var kb = (AttachmentSizeBytes(m) + 1023) / 1024;
            var hint = AttachmentActionHint(m);
            var body = string.IsNullOrEmpty(hint)
                ? $"[{kind}] {name} · {kb} КБ"
                : $"[{kind}] {name} · {kb} КБ · {hint}";
            return new ChatLine($"{who}: {body}{status}", m.Outgoing, m.Id, isAttachment: true);
        }

        return new ChatLine($"{who}: {m.Text}{status}", m.Outgoing, m.Id);
    }

    private static string FormatTitle(ChatEntity chat) =>
        $"{chat.PeerNickname} ({chat.PeerNetworkIdShort})";

    private static string FormatPreview(ChatMessageEntity? m)
    {
        if (m == null)
            return "";
        if (m.PayloadKind == (int)ChatPayloadKind.Image)
            return "[фото]";
        if (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            return "[голос]";
        if (m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.TransferOffer)
        {
            if (!string.IsNullOrWhiteSpace(m.TransferFileName))
                return m.TransferFileName;
            return string.IsNullOrWhiteSpace(m.Text) ? "[файл]" : m.Text;
        }

        return string.IsNullOrWhiteSpace(m.Text) ? "" : m.Text.Replace('\n', ' ');
    }

    private static string FormatDeliverySuffix(int deliveryStatus) =>
        (MessageDeliveryStatus)deliveryStatus switch
        {
            MessageDeliveryStatus.Pending => " ⌛",
            MessageDeliveryStatus.Sent => " ✓",
            MessageDeliveryStatus.Delivered => " ✓✓",
            MessageDeliveryStatus.Failed => " !",
            _ => ""
        };

    private async Task SendAsync()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0)
            return;
        var user = _auth.CurrentUser;
        if (user == null)
            return;

        EnsureSession(user);

        if (_p2pSession!.IsMeshtasticPathActive && text.Length > ChatP2PSession.MaxMeshtasticMessageChars)
        {
            var warn = string.Format(
                LocalizationUtils.GetStringByKey("chat.text_meshtastic_too_long"),
                ChatP2PSession.MaxMeshtasticMessageChars);
            MessageBox.Show(this, warn, LocalizationUtils.GetStringByKey("chat.send"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ShowSendError(warn);
            return;
        }

        _send.Enabled = false;
        try
        {
            // UI only enqueues Pending into DB; the session flush worker delivers from DB.
            await _p2pSession.SendTextAsync(text).ConfigureAwait(false);
            if (InvokeRequired)
                BeginInvoke(new Action(() => _input.Clear()));
            else
                _input.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send text");
            ShowSendError(ex.Message);
        }
        finally
        {
            if (IsHandleCreated && InvokeRequired)
                BeginInvoke(new Action(() => _send.Enabled = true));
            else
                _send.Enabled = true;
        }
    }

    private void ShowSendError(string text)
    {
        void Show() =>
            MessageBox.Show(this, text, "Отправка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        if (InvokeRequired)
            BeginInvoke(new Action(Show));
        else
            Show();
    }

    private void OnSidebarDrawItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _sidebarItems.Count)
            return;

        var row = _sidebarItems[e.Index];
        var unread = _unreadChatIds.Contains(row.Chat.Id);
        var baseFont = e.Font ?? _sidebar.Font;
        using var boldFont = unread || e.Index == _sidebar.SelectedIndex
            ? new Font(baseFont, FontStyle.Bold)
            : null;
        var nameFont = boldFont ?? baseFont;
        var nameBounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 2, e.Bounds.Width - 10, nameFont.Height);
        TextRenderer.DrawText(e.Graphics, row.Chat.PeerNickname, nameFont, nameBounds, e.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (!string.IsNullOrEmpty(row.Preview))
        {
            var previewBounds = new Rectangle(
                e.Bounds.X + 6,
                e.Bounds.Y + 2 + nameFont.Height,
                e.Bounds.Width - 10,
                baseFont.Height);
            TextRenderer.DrawText(e.Graphics, row.Preview, baseFont, previewBounds, PreviewColor,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        e.DrawFocusRectangle();
    }

    private void OnMessagesDrawItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _messages.Items.Count)
            return;

        var line = _messages.Items[e.Index] as ChatLine;
        var text = line?.Text ?? _messages.Items[e.Index]?.ToString() ?? "";
        var color = line is { Outgoing: false } ? PeerMessageColor : e.ForeColor;
        var font = e.Font ?? _messages.Font;
        var bounds = new Rectangle(
            e.Bounds.X + MessageTextPadX,
            e.Bounds.Y + MessageRowPad / 2,
            Math.Max(1, e.Bounds.Width - MessageTextPadX * 2),
            Math.Max(1, e.Bounds.Height - MessageRowPad));
        TextRenderer.DrawText(e.Graphics, text, font, bounds, color,
            TextFormatFlags.Left | MessageWrapFlags);
        e.DrawFocusRectangle();
    }

    private void OnMessagesMeasureItem(object? sender, MeasureItemEventArgs e)
    {
        var font = _messages.Font;
        var min = Math.Min(255, Math.Max(1, Math.Max(font.Height + MessageRowPad, 26)));
        var width = MessageTextWidth();
        if (width <= 0 || e.Index < 0 || e.Index >= _messages.Items.Count)
        {
            e.ItemHeight = min;
            return;
        }

        var line = _messages.Items[e.Index] as ChatLine;
        var text = line?.Text ?? _messages.Items[e.Index]?.ToString() ?? "";
        var measured = TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue),
            MessageWrapFlags);
        var h = measured.Height + MessageRowPad;
        if (h < min)
            h = min;
        else if (h > 255)
            h = 255;
        e.ItemHeight = h;
    }

    private int MessageTextWidth()
    {
        if (!_messages.IsHandleCreated)
            return 0;
        var w = _messages.ClientSize.Width - MessageTextPadX * 2;
        return w > 0 ? w : 0;
    }

    private void RelayoutMessageHeights()
    {
        if (!_messagesLayoutReady || _relayoutingMessages)
            return;
        if (!IsHandleCreated || !_messages.IsHandleCreated || _messages.RecreatingHandle)
            return;
        if (_messages.IsDisposed || _messages.DrawMode != DrawMode.OwnerDrawVariable)
            return;

        var w = _messages.ClientSize.Width;
        if (w <= 0 || w == _messagesLayoutWidth)
            return;

        _relayoutingMessages = true;
        try
        {
            _messagesLayoutWidth = w;
            if (_messages.Items.Count == 0)
                return;

            var top = _messages.TopIndex;
            var sel = _messages.SelectedIndex;
            var items = new object[_messages.Items.Count];
            _messages.Items.CopyTo(items, 0);
            _messages.BeginUpdate();
            try
            {
                _messages.Items.Clear();
                _messages.Items.AddRange(items);
                if (sel >= 0 && sel < _messages.Items.Count)
                    _messages.SelectedIndex = sel;
                if (top >= 0 && top < _messages.Items.Count)
                    _messages.TopIndex = top;
            }
            finally
            {
                _messages.EndUpdate();
            }
        }
        finally
        {
            _relayoutingMessages = false;
        }
    }

    private void OnMessagesMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        var idx = _messages.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= _messages.Items.Count)
            return;
        if (_messages.Items[idx] is not ChatLine { IsAttachment: true, MessageId: > 0 } line)
            return;
        _ = OpenOrDownloadAttachmentAsync(line.MessageId);
    }

    private async Task OpenOrDownloadAttachmentAsync(int messageId)
    {
        try
        {
            var row = await _chats.GetMessageAsync(messageId).ConfigureAwait(true);
            if (row == null || row.ChatId != _chat.Id)
            {
                ShowAttachmentError("Сообщение не найдено.");
                return;
            }

            if (row.ImageBlob is { Length: > 0 })
            {
                DisplayAttachment(row);
                return;
            }

            var canDownload = _p2pSession != null &&
                              !row.Outgoing &&
                              !string.IsNullOrWhiteSpace(row.TransferId);
            if (!canDownload)
            {
                ShowAttachmentError("Файл ещё недоступен для скачивания.");
                return;
            }

            QueueBinaryDownload(messageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open attachment failed");
            ShowAttachmentError(ex.Message);
        }
    }

    private void QueueBinaryDownload(int messageId)
    {
        var session = _p2pSession;
        if (session == null)
            return;
        if (!_binaryDownloadsInFlight.TryAdd(messageId, 0))
            return;
        _ = RunBinaryDownloadAsync(session, messageId);
    }

    private async Task RunBinaryDownloadAsync(ChatP2PSession session, int messageId)
    {
        try
        {
            await session.RequestBinaryDownloadAsync(messageId).ConfigureAwait(true);
            var row = await _chats.GetMessageAsync(messageId).ConfigureAwait(true);
            if (row?.ImageBlob is not { Length: > 0 })
            {
                await ReloadAsync().ConfigureAwait(true);
                return;
            }

            var ready = row;
            void Show()
            {
                if (IsDisposed)
                    return;
                DisplayAttachment(ready);
                _ = ReloadAsync();
            }

            if (IsHandleCreated && InvokeRequired)
                BeginInvoke(new Action(Show));
            else
                Show();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transfer download failed in chat {ChatId}", _chat.Id);
            ShowAttachmentError(ex.Message);
            await ReloadAsync().ConfigureAwait(true);
        }
        finally
        {
            _binaryDownloadsInFlight.TryRemove(messageId, out _);
        }
    }

    private void DisplayAttachment(ChatMessageEntity row)
    {
        if (row.ImageBlob is not { Length: > 0 } blob)
        {
            ShowAttachmentError("Файл ещё недоступен.");
            return;
        }

        var name = SanitizeFileName(EnsureMediaFileName(AttachmentDisplayName(row), row));
        if (IsImageAttachment(row) || IsVideoAttachment(row) || IsVoiceAttachment(row))
        {
            try
            {
                OpenWithAssociatedApp(blob, $"{row.Id}_{name}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Open media with associated app failed");
                ShowAttachmentError(ex.Message);
            }

            return;
        }

        using var sfd = new SaveFileDialog
        {
            Title = "Сохранить файл",
            FileName = name,
            Filter = "Все файлы|*.*",
            OverwritePrompt = true
        };
        if (sfd.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            File.WriteAllBytes(sfd.FileName, blob);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Save document failed");
            ShowAttachmentError(ex.Message);
        }
    }

    private static void OpenWithAssociatedApp(byte[] bytes, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "TorgLinkChat");
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, fileName);
        File.WriteAllBytes(path, bytes);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void ShowAttachmentError(string text)
    {
        void Show() =>
            MessageBox.Show(this, text, "Файл", MessageBoxButtons.OK, MessageBoxIcon.Information);
        if (InvokeRequired)
            BeginInvoke(new Action(Show));
        else
            Show();
    }

    private static bool IsAttachmentMessage(ChatMessageEntity m) =>
        m.PayloadKind is (int)ChatPayloadKind.File
            or (int)ChatPayloadKind.Image
            or (int)ChatPayloadKind.TransferOffer ||
        IsVoiceAttachment(m) ||
        IsVideoAttachment(m);

    private static bool IsLocallyAvailable(ChatMessageEntity m) =>
        m.ImageBlob is { Length: > 0 } || m.HasPayloadBlob;

    private static string AttachmentActionHint(ChatMessageEntity m)
    {
        if (IsLocallyAvailable(m))
            return IsImageAttachment(m) || IsVideoAttachment(m) || IsVoiceAttachment(m)
                ? "щёлкните — открыть"
                : "щёлкните — сохранить";

        if (m.Outgoing)
            return "";

        return (ChatTransferState)m.TransferState switch
        {
            ChatTransferState.Transferring => "загрузка...",
            ChatTransferState.Failed => "ошибка · щёлкните — скачать",
            _ => "щёлкните — скачать"
        };
    }

    private static string AttachmentKindLabel(ChatMessageEntity m)
    {
        if (IsVoiceAttachment(m))
            return "голос";
        if (IsImageAttachment(m))
            return "фото";
        if (IsVideoAttachment(m))
            return "видео";
        return "файл";
    }

    private static string AttachmentDisplayName(ChatMessageEntity m)
    {
        if (!string.IsNullOrWhiteSpace(m.TransferFileName))
            return m.TransferFileName;
        if (!string.IsNullOrWhiteSpace(m.Text))
            return m.Text;
        if (IsImageAttachment(m))
            return "image.jpg";
        if (IsVideoAttachment(m))
            return "video.mp4";
        if (IsVoiceAttachment(m))
            return "voice.ogg";
        return "file";
    }

    private static int AttachmentSizeBytes(ChatMessageEntity m) =>
        m.ImageBlob is { Length: > 0 } blob ? blob.Length : (int)m.TransferSizeBytes;

    private static bool IsVoiceAttachment(ChatMessageEntity m) =>
        string.Equals(m.TransferPayloadKind, "voice", StringComparison.OrdinalIgnoreCase) ||
        (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (m.TransferFileName?.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (m.Text?.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool IsImageAttachment(ChatMessageEntity m) =>
        m.PayloadKind == (int)ChatPayloadKind.Image ||
        string.Equals(m.TransferPayloadKind, "image", StringComparison.OrdinalIgnoreCase) ||
        (m.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool IsVideoAttachment(ChatMessageEntity m) =>
        string.Equals(m.TransferPayloadKind, "video", StringComparison.OrdinalIgnoreCase) ||
        (m.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ?? false);

    private static string EnsureMediaFileName(string name, ChatMessageEntity row)
    {
        if (Path.HasExtension(name))
            return name;
        if (IsImageAttachment(row))
        {
            var ext = row.MimeType?.Trim().ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
            return name + ext;
        }

        if (IsVideoAttachment(row))
            return name + ".mp4";
        if (IsVoiceAttachment(row))
            return name + ".ogg";
        return name;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';

        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "document" : s;
    }

    private sealed class SidebarRow(ChatEntity chat, string preview)
    {
        public ChatEntity Chat { get; } = chat;
        public string Preview { get; } = preview;

        public override string ToString() =>
            string.IsNullOrEmpty(Preview) ? Chat.PeerNickname : $"{Chat.PeerNickname} — {Preview}";
    }

    private sealed class ChatLine(string text, bool outgoing, int messageId = 0, bool isAttachment = false)
    {
        public string Text { get; } = text;
        public bool Outgoing { get; } = outgoing;
        public int MessageId { get; } = messageId;
        public bool IsAttachment { get; } = isAttachment;

        public override string ToString() => Text;
    }
}

public sealed class ChatSwitchedEventArgs(int oldChatId, int newChatId) : EventArgs
{
    public int OldChatId { get; } = oldChatId;
    public int NewChatId { get; } = newChatId;
}

public sealed class ChatSwitchRequestEventArgs(ChatEntity chat) : EventArgs
{
    public ChatEntity Chat { get; } = chat;
    public bool Handled { get; set; }
}
