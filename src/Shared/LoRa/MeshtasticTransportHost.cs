using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client;
using ShortP2P.Discovery;
using ShortP2P.Client.Services;
using ShortP2P.Client.Transport;
using ShortP2P.LoRa;
using ShortP2P.LoRa.Reliability;
using ShortP2P.LoRa.Directory;
using ShortP2P.LoRa.Jurisdiction;
using ShortP2P.LoRa.Link;
using ShortP2P.Transport.Abstractions;
using LoRaTransportKind = ShortP2P.LoRa.TransportKind;

namespace TorgLink.LoRa;

/// <summary>
///     TorgLink host for <see cref="MeshatasticTransport"/>: lifecycle, settings, MeshDirectory → chat endpoints.
///     Presence/discovery UDP is not carried on LoRa (REQ-WIRE-1); peers come from MeshDirectory snapshots.
/// </summary>
public sealed class MeshtasticTransportHost : IAdditionalTransportProvider, IAsyncDisposable
{
    /// <summary>SMT-1 / LoRa docs: client-to-client binary ceiling when LoRa is the active path.</summary>
    public const int MaxBinaryBytes = 20 * 1024;

    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly LoRaTransportSettingsStore _store;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _transportSync = new();

    private MeshatasticTransport? _transport;
    private MeshatasticTransportOptions? _activeOptions;
    private LoRaTransportSettings _settings = new();
    private readonly ConcurrentDictionary<string, LoRaDiscoveredPeerInfo> _discovered = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<uint, long> _lastPresenceReplyTicks = new();
    private Func<byte[], bool>? _isServerNetworkId;
    private int _directorySyncRunning;
    private CancellationTokenSource? _presenceLoopCts;
    private Task? _presenceLoopTask;

    /// <summary>SMT-1 / TRL-30: mesh presence cadence — own presence ping goes out once per minute.</summary>
    public static readonly TimeSpan PresenceBroadcastInterval = TimeSpan.FromMinutes(1);

    /// <summary>Online window: with 1-minute pings a peer stays online until two pings in a row are missed.</summary>
    private static readonly TimeSpan PresenceFreshness = TimeSpan.FromMinutes(2);

    private const long PresenceReplyIntervalMs = 20_000;

    /// <summary>Raised with the peer NetworkId (short form) when a mesh presence from it arrives.</summary>
    public event EventHandler<string>? PeerPresenceSeen;

    public MeshtasticTransportHost(
        AuthService auth,
        ChatRepository chats,
        LoRaTransportSettingsStore store,
        ILoggerFactory? loggerFactory = null)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _chats = chats ?? throw new ArgumentNullException(nameof(chats));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(nameof(MeshtasticTransportHost));
    }

    /// <summary>Raised after MeshDirectory content changes (UI refresh).</summary>
    public event EventHandler? DirectoryChanged;

    public LoRaTransportSettings CurrentSettings => _settings;

    public bool IsRunning
    {
        get
        {
            lock (_transportSync)
                return _transport != null;
        }
    }

    public MeshDirectory? Directory
    {
        get
        {
            lock (_transportSync)
                return _transport?.Directory;
        }
    }

    /// <summary>Optional: which NetworkIds are messenger servers (REQ-DIR-2 subject).</summary>
    public void SetServerNetworkIdLookup(Func<byte[], bool>? isServerNetworkId) =>
        _isServerNetworkId = isServerNetworkId;

    public async Task<LoRaTransportSettings> LoadSettingsAsync()
    {
        _settings = await _store.LoadAsync().ConfigureAwait(false);
        return _settings;
    }

    public async Task SaveSettingsAsync(LoRaTransportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _store.SaveAsync(settings).ConfigureAwait(false);
        _settings = settings;
    }

    /// <summary>
    ///     Start or restart the transport for the current user when Enabled + COM + jurisdiction allow it.
    /// </summary>
    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _settings = await _store.LoadAsync().ConfigureAwait(false);
            var user = _auth.CurrentUser;
            if (user == null || !_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ComPort))
            {
                await StopCoreAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            // Already running on the same board settings: keep the COM session open instead of
            // stop+start churn on every page appearing (the board resets on the DTR edge each reopen).
            lock (_transportSync)
            {
                if (_transport != null && ActiveOptionsMatch(_settings))
                    return;
            }

            var jurisdiction = JurisdictionCatalog.Find(_settings.JurisdictionCode) ?? JurisdictionCatalog.Unset;
            if (jurisdiction.IsUnset)
                _logger.LogInformation(
                    "LoRa jurisdiction is UNSET — TX limited to the default 1% duty cycle (REQ-RF-2)");

            // REQ-RF-2: TX 1% / RX 99% of estimated airtime over a rolling hour, never above the region cap.
            var regionCap = jurisdiction.DutyCycleMaxPercent;
            var dutyPercent = Math.Min(
                MeshtasticGate.DefaultDutyCyclePercent,
                regionCap > 0 ? regionCap : MeshtasticGate.DefaultDutyCyclePercent);
            await MeshtasticGate.Instance.ConfigureDutyCycleAsync(dutyPercent,
                DutyCycleBudget.DefaultAirtimePerByteMs, cancellationToken).ConfigureAwait(false);

            await StopCoreAsync(cancellationToken).ConfigureAwait(false);

            var options = new MeshatasticTransportOptions
            {
                ComPort = _settings.ComPort.Trim(),
                BaudRate = _settings.BaudRate > 0 ? _settings.BaudRate : 115200,
                Jurisdiction = jurisdiction,
                LocalNetworkId = CompressedNetworkId.FromShortString(user.NetworkIdShort).ToWireBytes(),
                IsServerNetworkId = net => _isServerNetworkId?.Invoke(net) == true
            };

            var transport = new MeshatasticTransport(options, _logger);
            transport.Directory.Changed += OnDirectoryChanged;
            transport.PresenceReceived += OnPresenceReceived;
            try
            {
                await transport.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                transport.PresenceReceived -= OnPresenceReceived;
                transport.Directory.Changed -= OnDirectoryChanged;
                await transport.DisposeAsync().ConfigureAwait(false);
                _logger.LogWarning(ex, "Failed to start Meshatastic transport on {ComPort}", options.ComPort);
                throw;
            }

            lock (_transportSync)
            {
                _transport = transport;
                _activeOptions = options;
            }

            StartPresenceLoop(transport);

            _logger.LogInformation("Meshatastic transport started on {ComPort} (node {Node})",
                options.ComPort, transport.LocalMeshId?.ToHex() ?? "?");
            QueueDirectorySync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        await StopPresenceLoopAsync().ConfigureAwait(false);

        MeshatasticTransport? transport;
        lock (_transportSync)
        {
            transport = _transport;
            _transport = null;
            _activeOptions = null;
        }

        if (transport == null)
            return;

        transport.PresenceReceived -= OnPresenceReceived;
        transport.Directory.Changed -= OnDirectoryChanged;
        try
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Meshatastic transport dispose failed");
        }
    }

    public bool IsEnabled(byte kind) =>
        kind == LoRaTransportKind.Meshtastic && _settings.Enabled && IsRunning;

    /// <summary>Mesh peers ping once per minute; online = presence within <see cref="PresenceFreshness" />.</summary>
    public bool IsPeerRecentlySeen(string networkIdShort) => HasRecentPeer(networkIdShort);

    public IReadOnlyList<ITransport> GetDataTransports()
    {
        lock (_transportSync)
        {
            if (_transport == null || !_settings.Enabled)
                return [];
            return [_transport];
        }
    }

    public ITransport? ResolveOutbound(TransportAddress destination)
    {
        if (destination.Kind != LoRaTransportKind.Meshtastic || !_settings.Enabled)
            return null;
        lock (_transportSync)
            return _transport;
    }

    public void RememberPeer(byte[] networkIdWire12, TransportAddress from)
    {
        if (from.Kind != LoRaTransportKind.Meshtastic)
            return;
        MeshatasticTransport? transport;
        lock (_transportSync)
            transport = _transport;
        if (transport == null)
            return;
        try
        {
            var meshId = MeshatasticTransportAddress.FromTransportAddress(from);
            transport.Remember(networkIdWire12, meshId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RememberPeer failed for MeshId");
        }
    }

    public IReadOnlyList<LoRaMeshPeerInfo> ListPeers()
    {
        var dir = Directory;
        if (dir == null)
            return [];

        var list = new List<LoRaMeshPeerInfo>();
        foreach (var b in dir.Snapshot())
        {
            if (b.Subject == MeshSubject.Self)
                continue;
            string shortId;
            try
            {
                shortId = CompressedNetworkId.FromWireBytes(b.NetworkId).ToShortString();
            }
            catch
            {
                shortId = b.NetworkIdHex;
            }

            list.Add(new LoRaMeshPeerInfo(
                shortId,
                b.MeshIdHex,
                b.Subject.ToString(),
                b.IsActive,
                b.Confirmed,
                b.LastSeenUtc));
        }

        return list;
    }

    /// <summary>Peers that answered with a presence ping (nickname + network id).</summary>
    public IReadOnlyList<LoRaDiscoveredPeerInfo> ListDiscoveredPeers()
    {
        return _discovered.Values
            .OrderBy(p => p.Nickname, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool HasRecentPeer(string? networkIdShort)
    {
        var id = ChatRepository.CanonicalPeerNetworkId(networkIdShort ?? "");
        return id.Length > 0
               && _discovered.TryGetValue(id, out var peer)
               && DateTime.UtcNow - peer.LastSeenUtc <= PresenceFreshness;
    }

    /// <summary>Meshtastic nodes the radio has heard, excluding this board.</summary>
    public IReadOnlyList<LoRaRadioNodeInfo> ListRadioNodes()
    {
        MeshatasticTransport? transport;
        lock (_transportSync)
            transport = _transport;
        if (transport == null)
            return [];

        uint? self = null;
        if (transport.LocalMeshId != null && transport.LocalMeshId.TryToNodeNumber(out var selfNode))
            self = selfNode;

        var list = new List<LoRaRadioNodeInfo>();
        foreach (var node in transport.RadioNodes)
        {
            if (self != null && node.NodeNumber == self.Value)
                continue;
            list.Add(new LoRaRadioNodeInfo(
                node.NodeNumber, node.NodeId, node.LongName, node.ShortName, node.DisplayName));
        }

        return list;
    }

    /// <summary>
    ///     Open the radio if needed, re-read its node database, and broadcast our directory
    ///     so other TorgLink nodes on the mesh can answer.
    /// </summary>
    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            try
            {
                await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Meshtastic scan could not open the radio");
                return;
            }
        }

        MeshatasticTransport? transport;
        lock (_transportSync)
            transport = _transport;
        if (transport == null)
            return;

        // Same card as the contacts scan (presence 0x31): our network id and nickname.
        // A TorgLink node that hears the broadcast answers with its own card.
        await SendOwnPresenceAsync(transport, MeshatasticTransport.BroadcastNode, cancellationToken)
            .ConfigureAwait(false);

        var listen = Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);
        var nodes = transport.RefreshRadioNodesAsync(cancellationToken).AsTask();
        try
        {
            await Task.WhenAll(listen, nodes).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Meshtastic node database refresh failed");
            if (!listen.IsCompleted)
                await listen.ConfigureAwait(false);
        }

        DirectoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool ActiveOptionsMatch(LoRaTransportSettings settings)
    {
        var active = _activeOptions;
        if (active == null)
            return false;
        var baud = settings.BaudRate > 0 ? settings.BaudRate : 115200;
        return string.Equals(active.ComPort.Trim(), settings.ComPort.Trim(), StringComparison.OrdinalIgnoreCase)
               && active.BaudRate == baud
               && string.Equals(
                   active.Jurisdiction?.Code,
                   (JurisdictionCatalog.Find(settings.JurisdictionCode) ?? JurisdictionCatalog.Unset).Code,
                   StringComparison.OrdinalIgnoreCase);
    }

    private void OnPresenceReceived(uint fromNode, uint toNode, byte[] payload)
    {
        if (!PresencePingCodec.TryParse(payload, out var networkId, out var nickname, out _, out _, out _))
            return;

        var user = _auth.CurrentUser;
        var idShort = networkId.ToShortString();
        if (user != null && ChatRepository.PeerNetworkIdsEqual(idShort, user.NetworkIdShort))
            return;

        var id = ChatRepository.CanonicalPeerNetworkId(idShort);
        if (id.Length == 0)
            return;

        var nick = string.IsNullOrWhiteSpace(nickname) ? idShort : nickname.Trim();
        var mesh = MeshId.FromNodeNumber(fromNode);
        _discovered[id] = new LoRaDiscoveredPeerInfo(idShort, nick, mesh.ToHex(), DateTime.UtcNow);
        _logger.LogInformation("Meshtastic presence from {Nick} {NetworkId} node {Node:X8}", nick, idShort, fromNode);

        PeerPresenceSeen?.Invoke(this, idShort);

        MeshatasticTransport? transport;
        lock (_transportSync)
            transport = _transport;
        try
        {
            transport?.Remember(networkId.ToWireBytes(), mesh);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to bind mesh id for {NetworkId}", idShort);
        }

        DirectoryChanged?.Invoke(this, EventArgs.Empty);

        // Answer once per sender. The next card from them is their reply and is not answered again.
        if (transport == null)
            return;

        var now = Environment.TickCount64;
        if (_lastPresenceReplyTicks.TryGetValue(fromNode, out var prev) && now - prev < PresenceReplyIntervalMs)
            return;
        _lastPresenceReplyTicks[fromNode] = now;

        var replyTo = fromNode;
        _ = Task.Run(async () =>
        {
            try
            {
                await SendOwnPresenceAsync(transport, replyTo, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Meshtastic presence reply to {Node:X8} failed", replyTo);
            }
        });
    }

    private async Task SendOwnPresenceAsync(MeshatasticTransport transport, uint node, CancellationToken cancellationToken)
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;

        var payload = PresencePingCodec.Build(
            CompressedNetworkId.FromShortString(user.NetworkIdShort),
            user.Nickname,
            user.DataUdpPort > 0 ? user.DataUdpPort : 17500);
        await transport.SendOpaqueAsync(node, payload, cancellationToken).ConfigureAwait(false);
    }

    // ---- periodic presence broadcast (SMT-1: once per minute) ----

    private void StartPresenceLoop(MeshatasticTransport transport)
    {
        _presenceLoopCts = new CancellationTokenSource();
        _presenceLoopTask = Task.Run(() => PresenceLoopAsync(transport, _presenceLoopCts.Token));
    }

    private async Task StopPresenceLoopAsync()
    {
        if (_presenceLoopCts != null)
        {
            try
            {
                await _presenceLoopCts.CancelAsync().ConfigureAwait(false);
            }
            catch
            {
                // already stopped
            }

            _presenceLoopCts.Dispose();
            _presenceLoopCts = null;
        }

        if (_presenceLoopTask != null)
        {
            try
            {
                await _presenceLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // normal stop
            }
            catch
            {
                // the loop logs its own failures; never block teardown
            }

            _presenceLoopTask = null;
        }
    }

    private async Task PresenceLoopAsync(MeshatasticTransport transport, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(PresenceBroadcastInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await SendOwnPresenceAsync(transport, MeshatasticTransport.BroadcastNode, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Mesh presence broadcast failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // stopped together with the transport
        }
    }

    private void OnDirectoryChanged()
    {
        DirectoryChanged?.Invoke(this, EventArgs.Empty);
        QueueDirectorySync();
    }

    private void QueueDirectorySync()
    {
        if (Interlocked.Exchange(ref _directorySyncRunning, 1) != 0)
            return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150).ConfigureAwait(false);
                await SyncDirectoryToChatsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "MeshDirectory → chat endpoints sync failed");
            }
            finally
            {
                Interlocked.Exchange(ref _directorySyncRunning, 0);
            }
        });
    }

    private async Task SyncDirectoryToChatsAsync()
    {
        var user = _auth.CurrentUser;
        var dir = Directory;
        if (user == null || dir == null)
            return;

        var byNetwork = new Dictionary<string, List<TransportAddress>>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in dir.Snapshot())
        {
            if (!b.IsActive || b.Subject == MeshSubject.Self)
                continue;
            string shortId;
            try
            {
                shortId = CompressedNetworkId.FromWireBytes(b.NetworkId).ToShortString();
            }
            catch
            {
                continue;
            }

            if (!byNetwork.TryGetValue(shortId, out var list))
            {
                list = [];
                byNetwork[shortId] = list;
            }

            list.Add(MeshatasticTransportAddress.ToTransportAddress(new MeshId(b.MeshId)));
        }

        foreach (var (shortId, endpoints) in byNetwork)
        {
            var chat = await _chats.FindChatByPeerNetworkIdAsync(user.Id, shortId).ConfigureAwait(false);
            if (chat == null)
                continue;
            if (await _chats.ReplaceChatTransportEndpointsOfKindAsync(
                    chat.Id, LoRaTransportKind.Meshtastic, endpoints).ConfigureAwait(false))
            {
                _chats.NotifyChatListChanged();
                _logger.LogDebug("Updated LoRa endpoints for chat {ChatId} peer {Peer}", chat.Id, shortId);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
