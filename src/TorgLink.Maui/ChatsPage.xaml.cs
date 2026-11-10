using System.Collections.ObjectModel;
using System.ComponentModel;
using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Discovery.Profile;
using TorgLink.Localization;

namespace TorgLink.Maui;

public partial class ChatsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly List<ChatListRowVm> _allRows = [];
    private readonly ObservableCollection<ChatListRowVm> _chatRows = [];
    private readonly ChatRepository _chats;
    private readonly ILogger<ChatsPage> _logger;
    private readonly MessengerServerManager _messengerServers;
    private readonly PeerBlacklist _blacklist;
    private readonly UserP2pRuntime _p2p;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private int _refreshPending;
    private IDispatcherTimer? _presenceRefreshTimer;
    private Task _connectivityTask = Task.CompletedTask;
    private string _search = "";
    private bool _profileHooksAttached;

    public ChatsPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p,
        MessengerServerManager messengerServers, PeerBlacklist blacklist,
        ILogger<ChatsPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        _messengerServers = messengerServers;
        _blacklist = blacklist;
        _logger = logger;
        ChatsCollection.ItemsSource = _chatRows;
    }

    private void OnChatListChangedFromInvite(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => _ = OnChatListChangedAsync());
    }

    private async Task OnChatListChangedAsync()
    {
        await RefreshAsync().ConfigureAwait(true);
        var u = _auth.CurrentUser;
        if (u == null)
            return;
        _ = EnsureSessionsAfterChatListChangedAsync(u);
    }

    private async Task EnsureSessionsAfterChatListChangedAsync(UserEntity u)
    {
        try
        {
            await _p2p.EnsureAllChatSessionsStartedAsync(u, _auth, _chats, null, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ensure chat sessions after list change");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Title = LocalizationUtils.GetStringByKey("tab.chats");
        SearchEntry.Placeholder = LocalizationUtils.GetStringByKey("search");
        EmptyChatsLabel.Text = LocalizationUtils.GetStringByKey("chats.empty");
        _chats.ChatListChanged -= OnChatListChangedFromInvite;
        _chats.ChatListChanged += OnChatListChangedFromInvite;
        _chats.ChatCreated -= OnChatCreated;
        _chats.ChatCreated += OnChatCreated;
        _chats.ChatMessageAppended -= OnChatMessageAppended;
        _chats.ChatMessageAppended += OnChatMessageAppended;
        _chats.ChatMessageDeliveryChanged -= OnChatMessageDeliveryChanged;
        _chats.ChatMessageDeliveryChanged += OnChatMessageDeliveryChanged;
        _p2p.LocalScan.ClientsChanged -= OnLanPresenceChanged;
        _p2p.LocalScan.ClientsChanged += OnLanPresenceChanged;
        _messengerServers.TrustThreatDetected -= OnMessengerServerTrustThreat;
        _messengerServers.TrustThreatDetected += OnMessengerServerTrustThreat;
        _blacklist.Changed -= OnBlacklistChanged;
        _blacklist.Changed += OnBlacklistChanged;
        AttachPeerProfileHooks();
        EnsurePresenceRefreshTimerStarted();
        var u = _auth.CurrentUser;
        if (u != null)
        {
            try
            {
                await _blacklist.EnsureLoadedAsync(u.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Load blacklist on chats page appearing");
            }
        }

        // Always paint from local SQLite first. P2P / servers / sessions must not gate the list.
        await RefreshAsync().ConfigureAwait(true);

        if (u != null)
            QueueConnectivity(u);
    }

    private void QueueConnectivity(UserEntity u)
    {
        if (!_connectivityTask.IsCompleted)
            return;
        _connectivityTask = EnsureConnectivityInBackgroundAsync(u);
    }

    private async Task EnsureConnectivityInBackgroundAsync(UserEntity u)
    {
        try
        {
            await P2pBootstrap.EnsureStartedAsync(_p2p, u, _logger).ConfigureAwait(false);
            AppLog.PeerConnected("p2p-runtime", u.NetworkIdShort);
            await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(false);
            await _p2p.EnsureAllChatSessionsStartedAsync(u, _auth, _chats, null).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(UpdatePeerOnlineFlags);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ensure P2P on chats page appearing");
        }
    }

    protected override void OnDisappearing()
    {
        _p2p.LocalScan.ClientsChanged -= OnLanPresenceChanged;
        _chats.ChatMessageAppended -= OnChatMessageAppended;
        _chats.ChatMessageDeliveryChanged -= OnChatMessageDeliveryChanged;
        _chats.ChatCreated -= OnChatCreated;
        _messengerServers.TrustThreatDetected -= OnMessengerServerTrustThreat;
        _blacklist.Changed -= OnBlacklistChanged;
        DetachPeerProfileHooks();
        if (_presenceRefreshTimer != null)
            _presenceRefreshTimer.Stop();
        base.OnDisappearing();
    }

    private void AttachPeerProfileHooks()
    {
        var store = _p2p.PeerProfiles;
        if (store == null || _profileHooksAttached)
            return;
        store.Changed -= OnPeerProfileChanged;
        store.Changed += OnPeerProfileChanged;
        _profileHooksAttached = true;
    }

    private void DetachPeerProfileHooks()
    {
        var store = _p2p.PeerProfiles;
        if (store != null)
            store.Changed -= OnPeerProfileChanged;
        _profileHooksAttached = false;
    }

    private void OnPeerProfileChanged(object? sender, PeerProfileChangedEventArgs e)
    {
        var id = e.NetworkId.ToShortString();
        MainThread.BeginInvokeOnMainThread(() => _ = PatchPeerAvatarAsync(id));
    }

    private async Task PatchPeerAvatarAsync(string peerNetworkIdShort)
    {
        try
        {
            var store = _p2p.PeerProfiles;
            if (store == null)
                return;
            var snap = await store.GetAsync(CompressedNetworkId.FromShortString(peerNetworkIdShort))
                .ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var row in _allRows)
                {
                    if (!ChatRepository.PeerNetworkIdsEqual(row.PeerNetworkIdShort, peerNetworkIdShort))
                        continue;
                    row.SetAvatar(snap?.Avatar);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Patch peer avatar for {NetworkId}", peerNetworkIdShort);
        }
    }

    private void OnBlacklistChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => _ = RefreshAsync());
    }

    private void OnChatMessageAppended(object? sender, ChatMessageAppendedEventArgs e)
    {
        var chatId = e.ChatId;
        _ = PatchLastMessageAsync(chatId);
    }

    private void OnChatMessageDeliveryChanged(object? sender, ChatMessageAppendedEventArgs e) =>
        OnChatMessageAppended(sender, e);

    private void OnMessengerServerTrustThreat(object? sender, MessengerServerTrustThreatEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await DisplayAlert(
                LocalizationUtils.GetStringByKey("security.threat_title"),
                LocalizationUtils.GetStringByKeyWithFormat("security.threat_body", e.Server.BaseUrl, e.ExpectedFingerprint, e.ActualFingerprint),
                LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        });
    }

    private void OnLanPresenceChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(UpdatePeerOnlineFlags);
    }

    private void UpdatePeerOnlineFlags()
    {
        foreach (var row in _allRows)
            row.IsPeerOnline = _p2p.LocalScan.IsPeerSeenRecentlyOnLan(row.Chat.PeerNetworkIdShort);
    }

    private void EnsurePresenceRefreshTimerStarted()
    {
        _presenceRefreshTimer ??= Dispatcher.CreateTimer();
        _presenceRefreshTimer.Interval = TimeSpan.FromSeconds(5);
        _presenceRefreshTimer.Tick -= OnPresenceRefreshTimerTick;
        _presenceRefreshTimer.Tick += OnPresenceRefreshTimerTick;
        if (!_presenceRefreshTimer.IsRunning)
            _presenceRefreshTimer.Start();
    }

    private void OnPresenceRefreshTimerTick(object? sender, EventArgs e)
    {
        UpdatePeerOnlineFlags();
    }

    private async Task RefreshAsync()
    {
        if (!await _refreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            Interlocked.Exchange(ref _refreshPending, 1);
            return;
        }

        try
        {
            do
            {
                Interlocked.Exchange(ref _refreshPending, 0);
                await RefreshCoreAsync().ConfigureAwait(true);
            } while (Interlocked.Exchange(ref _refreshPending, 0) == 1);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync()
    {
        var u = _auth.CurrentUser;
        if (u == null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _chats.ChatListChanged -= OnChatListChangedFromInvite;
                _chats.ChatMessageAppended -= OnChatMessageAppended;
                _chats.ChatMessageDeliveryChanged -= OnChatMessageDeliveryChanged;
                _p2p.LocalScan.ClientsChanged -= OnLanPresenceChanged;
                Application.Current!.MainPage =
                    new NavigationPage(MauiProgram.Services.GetRequiredService<LoginPage>());
            }).ConfigureAwait(false);
            return;
        }

        // Local SQLite only — never load ImageBlob; presence is the green dot.
        var list = await _chats.ListChatsAsync(u.Id).ConfigureAwait(false);
        var built = new List<ChatListRowVm>(list.Count);
        foreach (var c in list)
        {
            await TrySyncChatNicknameFromLanAsync(c).ConfigureAwait(false);
            var lastPage = await _chats.ListMessagesPageDescAsync(c.Id, 0, 1, includePayloadBlob: false)
                .ConfigureAwait(false);
            var last = lastPage.Count > 0 ? lastPage[0] : null;
            built.Add(new ChatListRowVm(c, last, _p2p.LocalScan.IsPeerSeenRecentlyOnLan(c.PeerNetworkIdShort)));
        }

        await ApplyPeerAvatarsAsync(built).ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Header.Bind(u, _p2p);
            _allRows.Clear();
            _allRows.AddRange(built);
            ApplyFilter();
        }).ConfigureAwait(false);
    }

    private async Task ApplyPeerAvatarsAsync(IReadOnlyList<ChatListRowVm> rows)
    {
        var store = _p2p.PeerProfiles;
        if (store == null || rows.Count == 0)
            return;

        foreach (var row in rows)
        {
            try
            {
                var snap = await store.GetAsync(CompressedNetworkId.FromShortString(row.PeerNetworkIdShort))
                    .ConfigureAwait(false);
                row.SetAvatar(snap?.Avatar);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Load peer avatar for chat list {NetworkId}", row.PeerNetworkIdShort);
            }
        }
    }

    private async Task PatchLastMessageAsync(int chatId)
    {
        try
        {
            var lastPage = await _chats.ListMessagesPageDescAsync(chatId, 0, 1, includePayloadBlob: false)
                .ConfigureAwait(false);
            var last = lastPage.Count > 0 ? lastPage[0] : null;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var row = _allRows.Find(r => r.Chat.Id == chatId);
                if (row == null)
                {
                    _ = RefreshAsync();
                    return;
                }

                row.UpdateLast(last);
                var idx = _allRows.IndexOf(row);
                if (idx > 0)
                {
                    _allRows.RemoveAt(idx);
                    _allRows.Insert(0, row);
                }

                ApplyFilter();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Patch chat row {ChatId}", chatId);
        }
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

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _search = e.NewTextValue?.Trim() ?? "";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<ChatListRowVm> src = _allRows.Where(r =>
            !_blacklist.IsBlocked(_auth.CurrentUser?.Id, r.PeerNetworkIdShort));
        if (_search.Length > 0)
            src = src.Where(r =>
                r.PeerNickname.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                r.PeerNetworkIdShort.Contains(_search, StringComparison.OrdinalIgnoreCase));

        SyncChatRows(src.ToList());
    }

    private void SyncChatRows(IReadOnlyList<ChatListRowVm> desired)
    {
        for (var i = _chatRows.Count - 1; i >= 0; i--)
        {
            var id = _chatRows[i].Chat.Id;
            if (!desired.Any(d => d.Chat.Id == id))
                _chatRows.RemoveAt(i);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var want = desired[i];
            var existing = -1;
            for (var j = 0; j < _chatRows.Count; j++)
            {
                if (_chatRows[j].Chat.Id != want.Chat.Id)
                    continue;
                existing = j;
                break;
            }

            if (existing < 0)
            {
                _chatRows.Insert(i, want);
                continue;
            }

            if (existing != i)
                _chatRows.Move(existing, i);
            _chatRows[i].CopyFrom(want);
        }
    }

    private async void OnAddChatClicked(object? sender, EventArgs e)
    {
        var page = MauiProgram.Services.GetRequiredService<AddChatPage>();
        await Navigation.PushModalAsync(new NavigationPage(page)).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private void OnChatRowLoaded(object? sender, EventArgs e)
    {
        if (sender is not View rowRoot)
            return;
        ListRowHighlight.Attach(rowRoot);
        ChatListContextMenu.EnsureWired(rowRoot, CreateChatMenuDeps());
    }

    private ChatListContextMenu.Deps CreateChatMenuDeps() => new()
    {
        Host = this,
        Auth = _auth,
        Blacklist = _blacklist,
        MessengerServers = _messengerServers,
        Chats = _chats,
        P2p = _p2p,
        AfterChange = RefreshAsync
    };

    private async void OnChatSwipeDelete(object? sender, EventArgs e)
    {
        var row = ChatListContextMenu.FindRow(sender as Element);
        if (row == null)
            return;
        await ChatListContextMenu.DeleteChatAsync(row, CreateChatMenuDeps()).ConfigureAwait(true);
    }

    private void OnChatCreated(object? sender, ChatCreatedEventArgs e)
    {
        // Log chat creation and refresh UI on main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AppLog.ChatCreated(e.ChatId, e.Remote);
            _ = RefreshAsync();
        });
    }

    private async void OnBlockChatClicked(object? sender, EventArgs e)
    {
        ChatEntity? chat = null;
        for (var p = sender as Element; p != null; p = p.Parent)
            if (p.BindingContext is ChatListRowVm row)
            {
                chat = row.Chat;
                break;
            }

        var u = _auth.CurrentUser;
        if (chat == null || u == null)
            return;

        await BlacklistUi.ConfirmAndBlockAsync(
                this, _blacklist, u.Id, chat.PeerNetworkIdShort, chat.PeerNickname,
                chat.IsBot, _messengerServers).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async void OnChatRowTapped(object? sender, TappedEventArgs e)
    {
        var walk = sender switch
        {
            TapGestureRecognizer tg => tg.Parent as Element,
            Element el => el,
            _ => null
        };
        if (ChatListContextMenu.ConsumeSuppressPrimaryTap(walk))
            return;

        var row = ChatListContextMenu.FindRow(walk);
        if (row == null)
            return;

        var chatId = row.Chat.Id;
        try
        {
            await ChatNav.OpenChatAsync(this, chatId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open chat {ChatId} failed", chatId);
            await DisplayAlert(LocalizationUtils.GetStringByKey("chats.open_failed"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
    }
}

public sealed class ChatListRowVm : INotifyPropertyChanged
{
    private bool _isPeerOnline;
    private string _initials;
    private Color _avatarColor;
    private ImageSource? _avatarImage;
    private int _avatarFingerprint;
    private string _lastPreview;
    private string _timeLabel;
    private string _deliveryGlyph;
    private Color _deliveryGlyphColor;
    private bool _showDelivery;

    public ChatListRowVm(ChatEntity chat, ChatMessageEntity? last, bool isPeerOnline)
    {
        Chat = chat;
        _isPeerOnline = isPeerOnline;
        _initials = TorgLinkTheme.Initials(chat.PeerNickname);
        _avatarColor = TorgLinkTheme.AvatarColor(chat.PeerNetworkIdShort);
        (_lastPreview, _timeLabel, _deliveryGlyph, _deliveryGlyphColor, _showDelivery) = FromLast(last);
    }

    public ChatEntity Chat { get; }
    public string PeerNickname => Chat.PeerNickname;
    public string PeerNetworkIdShort => Chat.PeerNetworkIdShort;
    public string Initials => _initials;
    public Color AvatarColor => _avatarColor;
    public ImageSource? AvatarImage => _avatarImage;
    public bool HasAvatar => _avatarImage != null;
    public bool ShowInitials => _avatarImage == null;
    public string LastPreview => _lastPreview;
    public string TimeLabel => _timeLabel;
    public string DeliveryGlyph => _deliveryGlyph;
    public Color DeliveryGlyphColor => _deliveryGlyphColor;
    public bool ShowDelivery => _showDelivery;

    public bool IsPeerOnline
    {
        get => _isPeerOnline;
        set
        {
            if (_isPeerOnline == value)
                return;
            _isPeerOnline = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPeerOnline)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetAvatar(byte[]? avatar)
    {
        var fingerprint = AvatarFingerprint(avatar);
        if (fingerprint == _avatarFingerprint)
            return;

        var had = _avatarImage != null;
        _avatarFingerprint = fingerprint;
        _avatarImage = PeerAvatarUi.ToImageSource(avatar);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvatarImage)));
        if (had != (_avatarImage != null))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAvatar)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowInitials)));
        }
    }

    private static int AvatarFingerprint(byte[]? avatar)
    {
        if (avatar is not { Length: > 0 } || avatar.Length > PeerProfileLimits.MaxAvatarDisplayBytes)
            return 0;
        unchecked
        {
            var h = (avatar.Length * 397) ^ avatar[0] ^ (avatar[^1] << 8);
            if (avatar.Length > 2)
                h ^= avatar[avatar.Length / 2] << 16;
            return h == 0 ? 1 : h;
        }
    }

    public void CopyFrom(ChatListRowVm other)
    {
        if (ReferenceEquals(this, other))
            return;
        UpdateLastPreview(other._lastPreview, other._timeLabel, other._deliveryGlyph, other._deliveryGlyphColor,
            other._showDelivery);
        ApplyPeerVisuals();
        if (!ReferenceEquals(_avatarImage, other._avatarImage) || _avatarFingerprint != other._avatarFingerprint)
        {
            _avatarFingerprint = other._avatarFingerprint;
            _avatarImage = other._avatarImage;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvatarImage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAvatar)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowInitials)));
        }

        IsPeerOnline = other.IsPeerOnline;
    }

    public void UpdateLast(ChatMessageEntity? last)
    {
        var (preview, time, glyph, glyphColor, show) = FromLast(last);
        UpdateLastPreview(preview, time, glyph, glyphColor, show);
        ApplyPeerVisuals();
    }

    private void ApplyPeerVisuals()
    {
        Set(ref _initials, TorgLinkTheme.Initials(Chat.PeerNickname), nameof(Initials));
        Set(ref _avatarColor, TorgLinkTheme.AvatarColor(Chat.PeerNetworkIdShort), nameof(AvatarColor));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PeerNickname)));
    }

    private void UpdateLastPreview(string preview, string time, string glyph, Color glyphColor, bool show)
    {
        Set(ref _lastPreview, preview, nameof(LastPreview));
        Set(ref _timeLabel, time, nameof(TimeLabel));
        Set(ref _deliveryGlyph, glyph, nameof(DeliveryGlyph));
        Set(ref _deliveryGlyphColor, glyphColor, nameof(DeliveryGlyphColor));
        if (_showDelivery != show)
        {
            _showDelivery = show;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowDelivery)));
        }
    }

    private void Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static (string Preview, string Time, string Glyph, Color GlyphColor, bool Show) FromLast(
        ChatMessageEntity? last)
    {
        if (last == null)
            return ("Нет сообщений", "", "", Colors.Transparent, false);

        var preview = ChatNav.Preview(last);
        var time = ChatNav.TimeLabel(last.SentUtcTicks);
        if (!last.Outgoing)
            return (preview, time, "", Colors.Transparent, false);

        var ds = (MessageDeliveryStatus)last.DeliveryStatus;
        if (ds == MessageDeliveryStatus.NotApplicable)
            ds = MessageDeliveryStatus.Delivered;
        return ds switch
        {
            MessageDeliveryStatus.Pending => (preview, time, "\u23f3", Color.FromArgb("#B8860B"), true),
            MessageDeliveryStatus.Sent => (preview, time, OutgoingDeliveryIndicators.Sent, TorgLinkTheme.Check, true),
            MessageDeliveryStatus.Failed => (preview, time, "!", Colors.Red, true),
            _ => (preview, time, OutgoingDeliveryIndicators.Delivered, TorgLinkTheme.Check, true)
        };
    }
}

