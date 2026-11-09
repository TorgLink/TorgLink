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

    /// <summary>
    ///     Mesh presence cadence. Keep-alives stay sparse so chat/handshake keep airtime under the
    ///     default TX 10% / RX 90% duty budget; 5 minutes leaves headroom for chunked frames.
    /// </summary>
    public static readonly TimeSpan PresenceBroadcastInterval = TimeSpan.FromMinutes(5);

    /// <summary>Online window: with 5-minute pings a peer stays online until two pings in a row are missed.</summary>
    private static readonly TimeSpan PresenceFreshness = TimeSpan.FromMinutes(12);

    private const long PresenceReplyIntervalMs = 60_000;

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
                    "LoRa jurisdiction is UNSET — TX limited to the default 10% duty cycle (REQ-RF-2)");

            // REQ-RF-2: TX 10% / RX 90% of estimated airtime over a rolling hour, never above the region cap.
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
                // Tb=60s, Timeout = Tb*(1+0.015*Nrx) for control/short/medium;
                // LargePayloadTimeout=180s for large text/binary chunk assemblies (≥1000 B).
                Retry = RetryConfig.FromTxDutyPercent(dutyPercent),
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
            // Let an in-flight chunk train finish before tearing down the COM session.
            if (!await transport.WaitForIdleAsync(TimeSpan.FromSeconds(8), cancellationToken)
                    .ConfigureAwait(false))
            {
                _logger.LogWarning(
                    "Stopping Meshtastic transport while TX still busy after idle wait");
            }

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

    public IReadOnlyList<TransportAddress> GetPeerAddresses(string networkIdShort)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(networkIdShort))
            return [];

        MeshDirectory? dir;
        lock (_transportSync)
            dir = _transport?.Directory;
        if (dir == null)
            return [];

        byte[] wire;
        try
        {
            wire = CompressedNetworkId.FromShortString(networkIdShort.Trim()).ToWireBytes();
        }
        catch
        {
            return [];
        }

        var destinations = dir.GetActiveDestinations(wire);
        if (destinations.Count > 0)
        {
            var list = new List<TransportAddress>(destinations.Count);
            foreach (var mesh in destinations)
                list.Add(MeshatasticTransportAddress.ToTransportAddress(mesh));
            return list;
        }

        // Presence may have the MeshId before a confirmed directory binding exists.
        var id = ChatRepository.CanonicalPeerNetworkId(networkIdShort);
        if (id.Length > 0 &&
            _discovered.TryGetValue(id, out var discovered) &&
            DateTime.UtcNow - discovered.LastSeenUtc <= PresenceFreshness &&
            TryParseMeshIdHex(discovered.MeshIdHex, out var fromPresence))
        {
            return [MeshatasticTransportAddress.ToTransportAddress(fromPresence)];
        }

        return [];
    }

    /// <summary>
    ///     Map a Meshtastic MeshId (radio layer) to the subscriber NetworkId that currently owns it.
    ///     Prefers confirmed MeshDirectory bindings; falls back to recent presence discovery.
    /// </summary>
    public bool TryResolvePeerNetworkId(TransportAddress from, out string networkIdShort)
    {
        networkIdShort = "";
        if (from.Kind != LoRaTransportKind.Meshtastic || !_settings.Enabled)
            return false;

        MeshId meshId;
        try
        {
            meshId = MeshatasticTransportAddress.FromTransportAddress(from);
        }
        catch
        {
            return false;
        }

        MeshDirectory? dir;
        lock (_transportSync)
            dir = _transport?.Directory;
        if (dir != null && dir.TryGetOwnerNetworkId(meshId, out var wire, out _))
        {
            try
            {
                networkIdShort = CompressedNetworkId.FromWireBytes(wire).ToShortString();
                return networkIdShort.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        // Unconfirmed presence hint only — never treat MeshId bytes as NetworkId.
        var meshHex = meshId.ToHex();
        var now = DateTime.UtcNow;
        foreach (var pair in _discovered)
        {
            if (now - pair.Value.LastSeenUtc > PresenceFreshness)
                continue;
            if (!string.Equals(pair.Value.MeshIdHex, meshHex, StringComparison.OrdinalIgnoreCase))
                continue;
            networkIdShort = ChatRepository.CanonicalPeerNetworkId(pair.Key);
            return networkIdShort.Length > 0;
        }

        return false;
    }

    private static bool TryParseMeshIdHex(string? hex, out MeshId meshId)
    {
        meshId = null!;
        if (string.IsNullOrWhiteSpace(hex) || hex.Length % 2 != 0)
            return false;
        try
        {
            var bytes = Convert.FromHexString(hex);
            if (bytes.Length is < MeshId.MinLength or > MeshId.MaxLength)
                return false;
            meshId = new MeshId(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

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
                if (transport.IsTxBusy)
                {
                    _logger.LogInformation(
                        "Skipping presence reply to {Node:X8} — chat/chunk TX is busy", replyTo);
                    return;
                }

                var wait = await MeshtasticGate.Instance
                    .PeekDutyWaitAsync(32, CancellationToken.None)
                    .ConfigureAwait(false);
                if (wait > TimeSpan.Zero)
                {
                    _logger.LogInformation(
                        "Skipping presence reply to {Node:X8} — duty cycle would defer TX for {Seconds:F0}s",
                        replyTo, wait.TotalSeconds);
                    return;
                }

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
                    // Presence is best-effort: never cut into an active chunk train or spent duty budget.
                    if (transport.IsTxBusy)
                    {
                        _logger.LogInformation(
                            "Skipping mesh presence broadcast — chat/chunk TX is busy");
                        continue;
                    }

                    var wait = await MeshtasticGate.Instance
                        .PeekDutyWaitAsync(32, cancellationToken)
                        .ConfigureAwait(false);
                    if (wait > TimeSpan.Zero)
                    {
                        _logger.LogInformation(
                            "Skipping mesh presence broadcast — duty cycle would defer TX for {Seconds:F0}s",
                            wait.TotalSeconds);
                        continue;
                    }

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
                shortId = ChatRepository.CanonicalPeerNetworkId(
                    CompressedNetworkId.FromWireBytes(b.NetworkId).ToShortString());
            }
            catch
            {
                continue;
            }

            if (shortId.Length == 0)
                continue;

            if (!byNetwork.TryGetValue(shortId, out var list))
            {
                list = [];
                byNetwork[shortId] = list;
            }

            list.Add(MeshatasticTransportAddress.ToTransportAddress(new MeshId(b.MeshId)));
        }

        var changed = false;

        // Write current owners.
        foreach (var (shortId, endpoints) in byNetwork)
        {
            var chat = await _chats.FindChatByPeerNetworkIdAsync(user.Id, shortId).ConfigureAwait(false);
            if (chat == null)
                continue;
            if (await _chats.ReplaceChatTransportEndpointsOfKindAsync(
                    chat.Id, LoRaTransportKind.Meshtastic, endpoints).ConfigureAwait(false))
            {
                changed = true;
                _logger.LogDebug("Updated LoRa endpoints for chat {ChatId} peer {Peer}", chat.Id, shortId);
            }
        }

        // Only drop a stored MeshId when the directory shows it owned by a *different* NetworkId.
        // Temporary absence (presence skipped under duty cycle / TX busy) must not wipe endpoints —
        // that left peer endpoints=0 and ChatP2pSession rejecting 0x01/0x02 ("endpoint not accepted").
        var meshOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (shortId, endpoints) in byNetwork)
        {
            foreach (var ep in endpoints)
            {
                var hex = Convert.ToHexString(ep.Data);
                meshOwner[hex] = shortId;
            }
        }

        var chats = await _chats.ListChatsAsync(user.Id).ConfigureAwait(false);
        foreach (var chat in chats)
        {
            var peerShort = ChatRepository.CanonicalPeerNetworkId(chat.PeerNetworkIdShort);
            if (peerShort.Length == 0)
                continue;

            var existing = PeerTransportEndpoints.Parse(chat)
                .Where(ep => ep.Kind == LoRaTransportKind.Meshtastic)
                .ToList();
            if (existing.Count == 0)
                continue;

            var kept = new List<TransportAddress>(existing.Count);
            var removed = false;
            foreach (var ep in existing)
            {
                var hex = Convert.ToHexString(ep.Data);
                if (meshOwner.TryGetValue(hex, out var owner) &&
                    !ChatRepository.PeerNetworkIdsEqual(owner, peerShort))
                {
                    removed = true;
                    continue;
                }

                kept.Add(ep);
            }

            if (!removed)
                continue;

            if (await _chats.ReplaceChatTransportEndpointsOfKindAsync(
                    chat.Id, LoRaTransportKind.Meshtastic, kept).ConfigureAwait(false))
            {
                changed = true;
                _logger.LogDebug(
                    "Pruned moved LoRa endpoints for chat {ChatId} peer {Peer} (kept {Kept})",
                    chat.Id, peerShort, kept.Count);
            }
        }

        if (changed)
            _chats.NotifyChatListChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
