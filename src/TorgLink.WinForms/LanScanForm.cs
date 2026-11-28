using System.Linq;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Discovery;
using ShortP2P.MessengerServer.Contracts.Dtos;
using ShortP2P.Transport;
using ShortP2P.Transport.Abstractions;
using TorgLink.Localization;

namespace TorgLink.WinForms;

/// <summary>
/// Contacts-style list: existing chats + discovered peers (UDP LAN + messenger GetClients). No BLE.
/// </summary>
public sealed partial class LanScanForm : AppForm
{
    private sealed class ContactRow
    {
        public ChatEntity? Chat { get; init; }
        public DiscoveredLocalPeer? Peer { get; init; }
        public required string Name { get; init; }
        public required string NetworkId { get; init; }
        public required string Transport { get; init; }
        public required string Bot { get; init; }
        public bool IsBot { get; init; }
        public required string Status { get; init; }
        public required string AboutMe { get; init; }
        public required string LastSeen { get; init; }
    }

    private readonly AuthService _auth = null!;
    private readonly ChatRepository _chats = null!;
    private readonly LocalNetworkScanner _scanner = null!;
    private readonly MessengerServerSyncService _sync = null!;
    private readonly MessengerServerManager _manager = null!;
    private readonly IUdpTransportFactory _udpFactory = null!;
    private readonly P2pRoutingSettings _settings = null!;
    private readonly ILogger<LanScanForm> _logger = null!;
    private readonly Action<ChatEntity>? _openChat;
    private readonly Func<Task>? _refreshChats;

    private readonly List<ContactRow> _rows = [];
    private string _searchQuery = string.Empty;
    private bool _scanning;

    public LanScanForm()
    {
        InitializeComponent();
    }

    public LanScanForm(
        AuthService auth,
        ChatRepository chats,
        LocalNetworkScanner scanner,
        MessengerServerSyncService sync,
        MessengerServerManager manager,
        IUdpTransportFactory udpFactory,
        P2pRoutingSettings settings,
        ILogger<LanScanForm> logger,
        Action<ChatEntity>? openChat = null,
        Func<Task>? refreshChats = null)
        : this()
    {
        _auth = auth;
        _chats = chats;
        _scanner = scanner;
        _sync = sync;
        _manager = manager;
        _udpFactory = udpFactory;
        _settings = settings;
        _logger = logger;
        _openChat = openChat;
        _refreshChats = refreshChats;

        _scan.Click += async (_, _) => await OnScanAsync().ConfigureAwait(true);
        _list.ItemActivate += async (_, _) => await OnActivateAsync().ConfigureAwait(true);
        _search.TextChanged += (_, _) =>
        {
            _searchQuery = _search.Text.Trim();
            BindList();
        };

        ApplyLocalizedUi();
        // Known chats (+ already-discovered peers). No GetClients/LAN probe until Scan.
        Shown += async (_, _) =>
        {
            SetStatus("");
            await RefreshAsync().ConfigureAwait(true);
        };
    }

    protected override void ApplyLocalizedUi()
    {
        Text = LocalizationUtils.GetStringByKey("contacts.title");
        _hint.Text = LocalizationUtils.GetStringByKey("lan.hint");
        _colName.Text = LocalizationUtils.GetStringByKey("contact.nickname");
        _colBot.Text = LocalizationUtils.GetStringByKey("contact.bot");
        _colBot.Width = 420;
        _colNetworkId.Text = LocalizationUtils.GetStringByKey("contact.network_id");
        _colAbout.Text = LocalizationUtils.GetStringByKey("contact.about");
        _colLastSeen.Text = LocalizationUtils.GetStringByKey("lan.last");
        _scan.Text = LocalizationUtils.GetStringByKey("lan.scan");
        _close.Text = LocalizationUtils.GetStringByKey("close");
        _colTransport.Text = "Transport";
        _colStatus.Text = LocalizationUtils.GetStringByKey("online");
    }

    private async Task RefreshAsync()
    {
        var u = _auth.CurrentUser;
        _rows.Clear();
        if (u == null)
        {
            BindList();
            return;
        }

        var chats = await _chats.ListChatsAsync(u.Id).ConfigureAwait(true);
        var versions = await _sync.DescribeBotVersionsAsync().ConfigureAwait(true);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in chats)
        {
            var id = ChatRepository.CanonicalPeerNetworkId(c.PeerNetworkIdShort);
            seen.Add(id);
            await TrySyncChatNicknameFromLanAsync(c).ConfigureAwait(true);
            var peer = FindPeer(id);
            var isBot = c.IsBot || peer?.IsBot == true;
            var versionText = BotVersionText.Lookup(versions, id);
            _rows.Add(new ContactRow
            {
                Chat = c,
                Peer = peer,
                Name = c.PeerNickname,
                NetworkId = c.PeerNetworkIdShort,
                Transport = peer != null ? FormatTransport(peer.TransportKind) : "чат",
                Bot = BotCell(isBot, versionText),
                IsBot = isBot,
                Status = isBot ? "—" : IsOnline(id, peer) ? "онлайн" : "офлайн",
                AboutMe = TrimAbout(peer?.AboutMe, 40),
                LastSeen = isBot || peer == null ? "—" : peer.LastSeenUtc.ToLocalTime().ToString("T")
            });
        }

        foreach (var p in _scanner.Clients)
        {
            var id = ChatRepository.CanonicalPeerNetworkId(p.NetworkId.ToShortString());
            if (id.Length == 0 || seen.Contains(id))
                continue;
            seen.Add(id);
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            var versionText = BotVersionText.Lookup(versions, id);
            _rows.Add(new ContactRow
            {
                Peer = p,
                Name = nick,
                NetworkId = id,
                Transport = FormatTransport(p.TransportKind),
                Bot = BotCell(p.IsBot, versionText),
                IsBot = p.IsBot,
                Status = p.IsBot ? "—" : IsOnline(id, p) ? "онлайн" : "офлайн",
                AboutMe = TrimAbout(p.AboutMe, 40),
                LastSeen = p.IsBot ? "—" : p.LastSeenUtc.ToLocalTime().ToString("T")
            });
        }

        BindList();
    }

    private void BindList()
    {
        EnsureListColumns();
        IEnumerable<ContactRow> visible = _rows;
        if (_searchQuery.Length > 0)
            visible = _rows.Where(r =>
                r.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                r.NetworkId.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Bot.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));

        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach (var r in visible)
            {
                var item = new ListViewItem(r.Name) { Tag = r, ToolTipText = r.Bot };
                item.SubItems.Add(r.Bot);
                item.SubItems.Add(r.NetworkId);
                item.SubItems.Add(r.Transport);
                item.SubItems.Add(r.Status);
                item.SubItems.Add(r.AboutMe);
                item.SubItems.Add(r.LastSeen);
                if (r.IsBot)
                {
                    item.BackColor = Color.FromArgb(255, 236, 140);
                    item.ForeColor = Color.Black;
                }

                _list.Items.Add(item);
            }
        }
        finally
        {
            _list.EndUpdate();
        }
    }

    /// <summary>
    /// Designer sometimes rewrites InitializeComponent and drops Detail columns;
    /// without them View=Details shows a blank grid even when Items are populated.
    /// </summary>
    private void EnsureListColumns()
    {
        if (_list.View != View.Details)
            _list.View = View.Details;
        if (_list.Columns.Count > 0)
            return;

        _colName = new ColumnHeader { Text = "Имя", Width = 160 };
        _colBot = new ColumnHeader { Text = LocalizationUtils.GetStringByKey("contact.bot"), Width = 420 };
        _colNetworkId = new ColumnHeader { Text = "Network id", Width = 180 };
        _colTransport = new ColumnHeader { Text = "Транспорт", Width = 100 };
        _colStatus = new ColumnHeader { Text = "Статус", Width = 80 };
        _colAbout = new ColumnHeader { Text = "О себе", Width = 160 };
        _colLastSeen = new ColumnHeader { Text = "Последний контакт", Width = 140 };
        _list.Columns.AddRange([_colName, _colBot, _colNetworkId, _colTransport, _colStatus, _colAbout, _colLastSeen]);
    }

    private async Task TrySyncChatNicknameFromLanAsync(ChatEntity chat)
    {
        if (!ChatRepository.IsPlaceholderNickname(chat.PeerNickname, chat.PeerNetworkIdShort))
            return;

        var id = ChatRepository.CanonicalPeerNetworkId(chat.PeerNetworkIdShort);
        foreach (var p in _scanner.Clients)
        {
            if (!ChatRepository.PeerNetworkIdsEqual(p.NetworkId.ToShortString(), id))
                continue;
            var nick = p.Nickname?.Trim() ?? "";
            if (ChatRepository.IsPlaceholderNickname(nick, id))
                continue;
            if (await _chats.TryUpdatePeerNicknameAsync(chat.Id, nick).ConfigureAwait(true))
                chat.PeerNickname = nick;
            return;
        }
    }

    private DiscoveredLocalPeer? FindPeer(string networkIdShort)
    {
        foreach (var c in _scanner.Clients)
        {
            if (ChatRepository.PeerNetworkIdsEqual(c.NetworkId.ToShortString(), networkIdShort))
                return c;
        }

        return null;
    }

    private bool IsOnline(string networkIdShort, DiscoveredLocalPeer? peer)
    {
        if (peer?.TransportKind == TransportKind.MessengerServer)
            return peer.MessengerServerOnline;
        return _scanner.IsPeerSeenRecentlyOnLan(networkIdShort) ||
               (peer?.MessengerServerOnline ?? false) ||
               _scanner.Clients.Any(c =>
                   ChatRepository.PeerNetworkIdsEqual(c.NetworkId.ToShortString(), networkIdShort) &&
                   c.MessengerServerOnline);
    }

    private static string FormatTransport(byte k) =>
        k switch
        {
            TransportKind.Udp => "LAN",
            TransportKind.Bluetooth => "Bluetooth",
            TransportKind.Infrared => "IrDA",
            TransportKind.MessengerServer => "Сервер",
            _ => k.ToString()
        };

    private void SetStatus(string text)
    {
        _status.Text = text;
        _status.Visible = text.Length > 0;
    }

    private static string BotCell(bool isBot, string versions)
    {
        if (versions.Length > 0)
            return versions;
        return isBot ? LocalizationUtils.GetStringByKey("contact.bot_mark") : "";
    }

    private static string FormatStatusSummary(int serverClients, int bots) =>
        $"Сервер: {serverClients} клиент(ов), {bots} бот(ов). Нажмите «Сканировать» для полного LAN-раунда.";

    private async Task OnActivateAsync()
    {
        if (_list.SelectedItems.Count == 0)
            return;
        if (_list.SelectedItems[0].Tag is not ContactRow row)
            return;

        try
        {
            if (row.Chat != null)
            {
                if (_refreshChats != null)
                    await _refreshChats().ConfigureAwait(true);
                _openChat?.Invoke(row.Chat);
                return;
            }

            if (row.Peer == null)
                return;

            var ctx = new LanChatStartContext
            {
                MessengerServers = _sync,
                UdpTransportFactory = _udpFactory,
                Settings = _settings
            };
            var result = await LanChatStartFromDiscovery
                .TryStartAsync(row.Peer, _auth, _chats, ctx, CancellationToken.None).ConfigureAwait(true);

            switch (result.Kind)
            {
                case LanChatStartKind.AlreadyExists:
                case LanChatStartKind.Created:
                    if (_refreshChats != null)
                        await _refreshChats().ConfigureAwait(true);
                    if (result.Chat != null)
                        _openChat?.Invoke(result.Chat);
                    await RefreshAsync().ConfigureAwait(true);
                    break;
                case LanChatStartKind.WaitingForPeer:
                    MessageBox.Show(this, result.Message ?? "", "Контакты", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;
                case LanChatStartKind.Failed:
                    MessageBox.Show(this, result.Message ?? "Ошибка", "Контакты", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Contacts item activate");
            MessageBox.Show(this, ex.Message, "Контакты", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task OnScanAsync()
    {
        if (_scanning)
            return;

        _scanning = true;
        _scan.Enabled = false;
        var sec = (int)Math.Round(LocalNetworkScanner.DefaultScanListenDuration.TotalSeconds);
        try
        {
            // Explicit GetClients + GetBots first (do not rely only on swallowed PrioritizedExternalDiscoveryRound).
            SetStatus("Опрос messenger-серверов (GetClients, боты)…");
            var remote = await ProbeGetClientsAsync().ConfigureAwait(true);
            _scanner.ApplyMessengerServerDirectory(Program.ToDirectoryEntries(remote));
            var bots = await ProbeBotsAsync().ConfigureAwait(true);
            if (bots != null)
                _scanner.ApplyBotDirectory(MessengerServerSyncService.ToBotDirectoryEntries(bots));
            await RefreshAsync().ConfigureAwait(true);
            SetStatus($"Сервер: {remote.Count} клиент(ов), {bots?.Count ?? 0} бот(ов). Слушаем LAN {sec} с…");

            await _scanner.ScanAsync(LocalNetworkScanner.DefaultScanListenDuration).ConfigureAwait(true);

            // Refresh directory after LAN window (ScanAsync also runs the discovery hook).
            remote = await ProbeGetClientsAsync().ConfigureAwait(true);
            _scanner.ApplyMessengerServerDirectory(Program.ToDirectoryEntries(remote));
            bots = await ProbeBotsAsync().ConfigureAwait(true);
            if (bots != null)
                _scanner.ApplyBotDirectory(MessengerServerSyncService.ToBotDirectoryEntries(bots));
            await RefreshAsync().ConfigureAwait(true);
            SetStatus(bots == null
                ? "Список ботов не получен — проверьте «Серверы» (active/trusted)."
                : FormatStatusSummary(remote.Count, bots.Count));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Contacts scan");
            MessageBox.Show(this, ex.Message, "Контакты", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetStatus("");
        }
        finally
        {
            _scanning = false;
            _scan.Enabled = true;
        }
    }

    private async Task<IReadOnlyList<ClientPresenceDto>> ProbeGetClientsAsync()
    {
        try
        {
            _sync.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MessengerServers.Start before GetClients");
        }

        try
        {
            var remote = await _sync.ProbeAndListRemoteClientsAsync(CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("GetClients returned {Count} remote client(s)", remote.Count);
            return remote;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetClients probe failed");
            SetStatus("GetClients не удался — проверьте «Серверы» (active/trusted).");
            return Array.Empty<ClientPresenceDto>();
        }
    }

    private async Task<IReadOnlyList<BotCatalogDto>?> ProbeBotsAsync()
    {
        try
        {
            var bots = await _sync.ProbeAndListRemoteBotsAsync(CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("GetBots returned {Count} bot(s)", bots.Count);
            return bots;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetBots probe failed");
            return null;
        }
    }

    private static string TrimAbout(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var t = text.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }
}
