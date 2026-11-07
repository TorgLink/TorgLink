using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using TorgLink.Maui.Services;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Discovery;
using ShortP2P.Transport.Abstractions;
using TorgLink.Localization;
using TorgLink.LoRa;
using LoRaTransportKind = ShortP2P.LoRa.TransportKind;

namespace TorgLink.Maui;

public sealed class ContactRow
{
    public ChatEntity? Chat { get; init; }
    public DiscoveredLocalPeer? Peer { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required string Initials { get; init; }
    public required Color AvatarColor { get; init; }
    public bool IsOnline { get; init; }
    public bool IsBot { get; init; }
    public string BotMark { get; init; } = "";
    public bool MeshOnly { get; init; }
}

public partial class ContactsPage : ContentPage
{
    private readonly List<ContactRow> _allRows = [];
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly ILogger<ContactsPage> _logger;
    private readonly UserP2pRuntime _p2p;
    private readonly MeshtasticTransportHost _lora;
    private readonly ObservableCollection<ContactRow> _rows = [];
    private bool _scanning;
    private string _search = "";

    public ContactsPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p,
        MeshtasticTransportHost lora, ILogger<ContactsPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        _lora = lora;
        _logger = logger;
        ContactsCollection.ItemsSource = _rows;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Title = LocalizationUtils.GetStringByKey("tab.contacts");
        SearchEntry.Placeholder = LocalizationUtils.GetStringByKey("contacts.search");
        ScanButton.Text = LocalizationUtils.GetStringByKey("contacts.scan");
        EmptyContactsLabel.Text = LocalizationUtils.GetStringByKey("contacts.empty");
        var u = _auth.CurrentUser;
        if (u != null)
            _ = EnsureConnectivityAsync(u);

        // Known chats (+ already-discovered peers in memory). No LAN/server probe until Scan.
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task EnsureConnectivityAsync(UserEntity u)
    {
        try
        {
            await P2pBootstrap.EnsureStartedAsync(_p2p, u, _logger).ConfigureAwait(false);
            await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ensure P2P on contacts page appearing");
        }
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _search = e.NewTextValue?.Trim() ?? "";
        ApplyFilter();
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        if (_scanning)
            return;

        _scanning = true;
        ScanButton.IsEnabled = false;
        var sec = (int)Math.Round(LocalNetworkScanner.DefaultScanListenDuration.TotalSeconds);
        ScanStatusLabel.Text = LocalizationUtils.GetStringByKeyWithFormat("contacts.scanning_detail", sec);
        ScanSpinner.IsRunning = true;
        ScanStatusRow.IsVisible = true;
        try
        {
            await Task.WhenAll(
                _p2p.LocalScan.ScanAsync(LocalNetworkScanner.DefaultScanListenDuration),
                _lora.ScanAsync()).ConfigureAwait(true);
            if (_p2p.MessengerServers != null)
            {
                var bots = await _p2p.MessengerServers.ProbeAndListRemoteBotsAsync(CancellationToken.None)
                    .ConfigureAwait(true);
                _p2p.LocalScan.ApplyBotDirectory(MessengerServerSyncService.ToBotDirectoryEntries(bots));
                await _p2p.MessengerServers.SyncBotKeysFromCatalogAsync(bots, CancellationToken.None)
                    .ConfigureAwait(true);
            }

            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Contacts scan");
            await DisplayAlert(LocalizationUtils.GetStringByKey("tab.contacts"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
        finally
        {
            _scanning = false;
            ScanSpinner.IsRunning = false;
            ScanStatusRow.IsVisible = false;
            ScanStatusLabel.Text = "";
            ScanButton.IsEnabled = true;
        }
    }

    private async Task RefreshAsync()
    {
        var u = _auth.CurrentUser;
        if (u == null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Header.Bind(null, _p2p);
                _allRows.Clear();
                ApplyFilter();
            }).ConfigureAwait(false);
            return;
        }

        var chats = await _chats.ListChatsAsync(u.Id).ConfigureAwait(false);
        var versions = _p2p.MessengerServers == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await _p2p.MessengerServers.DescribeBotVersionsAsync().ConfigureAwait(false);
        var built = new List<ContactRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in chats)
        {
            var chatId = ChatRepository.CanonicalPeerNetworkId(c.PeerNetworkIdShort);
            seen.Add(chatId);
            await TrySyncChatNicknameFromLanAsync(c).ConfigureAwait(false);
            var chatPeer = FindPeer(chatId);
            var chatIsBot = c.IsBot || chatPeer?.IsBot == true;
            var versionText = BotVersionText.Lookup(versions, chatId);
            built.Add(new ContactRow
            {
                Chat = c,
                Peer = chatPeer,
                Name = c.PeerNickname,
                Detail = versionText.Length > 0 ? versionText : c.PeerNetworkIdShort,
                Initials = TorgLinkTheme.Initials(c.PeerNickname),
                AvatarColor = TorgLinkTheme.AvatarColor(c.PeerNetworkIdShort),
                IsOnline = chatIsBot ? false : IsOnline(c.PeerNetworkIdShort, chatPeer),
                IsBot = chatIsBot,
                BotMark = chatIsBot ? BotMarkText() : ""
            });
        }

        foreach (var p in _p2p.LocalScan.Clients)
        {
            var id = ChatRepository.CanonicalPeerNetworkId(p.NetworkId.ToShortString());
            if (id.Length == 0 || seen.Contains(id))
                continue;
            seen.Add(id);
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            var versionText = BotVersionText.Lookup(versions, id);
            built.Add(new ContactRow
            {
                Peer = p,
                Name = nick,
                Detail = versionText.Length > 0 ? versionText : $"{id} · {TransportLabel(p)}",
                Initials = TorgLinkTheme.Initials(nick),
                AvatarColor = TorgLinkTheme.AvatarColor(id),
                IsOnline = p.IsBot ? false : IsOnline(id, p),
                IsBot = p.IsBot,
                BotMark = p.IsBot ? BotMarkText() : ""
            });
        }

        foreach (var mesh in _lora.ListDiscoveredPeers())
        {
            var id = ChatRepository.CanonicalPeerNetworkId(mesh.NetworkIdShort);
            if (id.Length == 0 || !seen.Add(id))
                continue;
            var nick = string.IsNullOrWhiteSpace(mesh.Nickname) ? id : mesh.Nickname;
            DiscoveredLocalPeer? peer = null;
            try
            {
                peer = new DiscoveredLocalPeer(
                    CompressedNetworkId.FromShortString(id),
                    nick,
                    ShortP2P.LoRa.MeshatasticTransportAddress.ToTransportAddress(
                        new ShortP2P.LoRa.MeshId(Convert.FromHexString(mesh.MeshIdHex))),
                    LoRaTransportKind.Meshtastic,
                    new DateTimeOffset(DateTime.SpecifyKind(mesh.LastSeenUtc, DateTimeKind.Utc)),
                    0);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skip mesh peer {Id}", id);
            }

            built.Add(new ContactRow
            {
                Peer = peer,
                Name = nick,
                Detail = $"{id} · Meshtastic",
                Initials = TorgLinkTheme.Initials(nick),
                AvatarColor = TorgLinkTheme.AvatarColor(id),
                IsOnline = _lora.HasRecentPeer(id)
            });
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Header.Bind(u, _p2p);
            _allRows.Clear();
            _allRows.AddRange(built);
            ApplyFilter();
        }).ConfigureAwait(false);
    }

    private async Task TrySyncChatNicknameFromLanAsync(ChatEntity chat)
    {
        if (!ChatRepository.IsPlaceholderNickname(chat.PeerNickname, chat.PeerNetworkIdShort))
            return;

        var id = ChatRepository.CanonicalPeerNetworkId(chat.PeerNetworkIdShort);
        foreach (var p in _p2p.LocalScan.Clients)
        {
            if (!ChatRepository.PeerNetworkIdsEqual(p.NetworkId.ToShortString(), id))
                continue;
            var nick = p.Nickname?.Trim() ?? "";
            if (ChatRepository.IsPlaceholderNickname(nick, id))
                continue;
            if (await _chats.TryUpdatePeerNicknameAsync(chat.Id, nick).ConfigureAwait(false))
                chat.PeerNickname = nick;
            return;
        }
    }

    private DiscoveredLocalPeer? FindPeer(string networkIdShort)
    {
        foreach (var peer in _p2p.LocalScan.Clients)
        {
            if (ChatRepository.PeerNetworkIdsEqual(peer.NetworkId.ToShortString(), networkIdShort))
                return peer;
        }

        return null;
    }

    private static string BotMarkText() => LocalizationUtils.GetStringByKey("contact.bot_mark");

    private bool IsOnline(string networkIdShort, DiscoveredLocalPeer? peer)
    {
        if (peer?.TransportKind == TransportKind.MessengerServer)
            return peer.MessengerServerOnline;
        return _p2p.LocalScan.IsPeerSeenRecentlyOnLan(networkIdShort) ||
               _lora.HasRecentPeer(networkIdShort) ||
               (peer?.MessengerServerOnline ?? false) ||
               _p2p.LocalScan.Clients.Any(c =>
                   ChatRepository.PeerNetworkIdsEqual(c.NetworkId.ToShortString(), networkIdShort) &&
                   c.MessengerServerOnline);
    }

    private void ApplyFilter()
    {
        IEnumerable<ContactRow> src = _allRows;
        if (_search.Length > 0)
            src = _allRows.Where(r =>
                r.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                r.Detail.Contains(_search, StringComparison.OrdinalIgnoreCase));

        _rows.Clear();
        foreach (var row in src)
            _rows.Add(row);
    }

    private static string TransportLabel(DiscoveredLocalPeer p) =>
        p.TransportKind switch
        {
            TransportKind.Udp => "LAN",
            TransportKind.Bluetooth => "Bluetooth",
            TransportKind.MessengerServer => LocalizationUtils.GetStringByKey("network.servers"),
            _ => p.TransportKind.ToString()
        };

    private void OnContactRowLoaded(object? sender, EventArgs e)
    {
        if (sender is not View rowRoot)
            return;

        ApplyBotRowColor(rowRoot);
        rowRoot.BindingContextChanged += (_, _) => ApplyBotRowColor(rowRoot);
        ListRowHighlight.Attach(rowRoot);
    }

    private static void ApplyBotRowColor(View rowRoot)
    {
        var rest = rowRoot.BindingContext is ContactRow { IsBot: true }
            ? Color.FromArgb("#F6E27A")
            : Colors.Transparent;
        ListRowHighlight.SetRestColor(rowRoot, rest);
    }

    private async void OnContactRowTapped(object? sender, TappedEventArgs e)
    {
        var walk = sender switch
        {
            TapGestureRecognizer tg => tg.Parent as Element,
            Element el => el,
            _ => null
        };
        ContactRow? row = null;
        for (var el = walk; el != null; el = el.Parent as Element)
            if (el.BindingContext is ContactRow vm)
            {
                row = vm;
                break;
            }

        if (row == null)
            return;

        try
        {
            if (row.Chat != null)
                await ChatNav.OpenChatAsync(this, row.Chat.Id).ConfigureAwait(true);
            else if (row.MeshOnly)
                await DisplayAlert(LocalizationUtils.GetStringByKey("tab.contacts"), $"{row.Name}\n{row.Detail}",
                    LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            else if (row.Peer != null)
                await ChatNav.OpenDiscoveredPeerAsync(this, row.Peer).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("tab.contacts"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
    }
}

