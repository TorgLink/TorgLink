using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using ShortP2P.Auth.Data;
using ShortP2P.Transport.Abstractions;
using TorgLink.Localization;
using TorgLink.LoRa;
using LoRaTransportKind = ShortP2P.LoRa.TransportKind;

namespace TorgLink.Maui;

public sealed class LanScanRow
{
    public DiscoveredLocalPeer? Peer { get; init; }
    public string Nickname { get; init; } = "";
    public string NetworkIdShort { get; init; } = "";
    public string DetailLine { get; init; } = "";
    public bool IsPeerOnline { get; init; }

    public static LanScanRow From(DiscoveredLocalPeer p, bool isPeerOnline)
    {
        var idShort = p.NetworkId.ToShortString();
        var transport = p.TransportKind switch
        {
            TransportKind.Udp => "UDP",
            TransportKind.Bluetooth => "Bluetooth",
            TransportKind.Infrared => "IrDA",
            TransportKind.MessengerServer => LocalizationUtils.GetStringByKey("network.servers"),
            LoRaTransportKind.Meshtastic => "Meshtastic",
            _ => p.TransportKind.ToString()
        };
        var seen = p.LastSeenUtc.ToLocalTime().ToString("g");
        var presence = isPeerOnline ? LocalizationUtils.GetStringByKey("online") : LocalizationUtils.GetStringByKey("offline");
        var detail = LocalizationUtils.GetStringByKeyWithFormat("lan.detail", transport, presence, LocalizationUtils.GetStringByKeyWithFormat("lan.last", seen));
        if (!string.IsNullOrWhiteSpace(p.AboutMe))
        {
            var about = p.AboutMe.Trim();
            if (about.Length > 80)
                about = about[..80] + "…";
            detail = $"{detail}\n{about}";
        }

        return new LanScanRow
        {
            Peer = p,
            IsPeerOnline = isPeerOnline,
            Nickname = string.IsNullOrEmpty(p.Nickname) ? "—" : p.Nickname,
            NetworkIdShort = idShort,
            DetailLine = detail
        };
    }
}

public partial class LanScanPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly ILogger<LanScanPage> _logger;
    private readonly UserP2pRuntime _p2p;
    private readonly MeshtasticTransportHost _lora;
    private readonly ObservableCollection<LanScanRow> _rows = [];

    public LanScanPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p,
        MeshtasticTransportHost lora, ILogger<LanScanPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        _lora = lora;
        _logger = logger;
        PeerCollection.ItemsSource = _rows;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Title = LocalizationUtils.GetStringByKey("lan.title");
        HintLabel.Text = LocalizationUtils.GetStringByKey("lan.hint");
        ScanButton.Text = LocalizationUtils.GetStringByKey("lan.scan");
        _p2p.LocalScan.ClientsChanged += OnClientsChanged;
        RefreshRows();
    }

    protected override void OnDisappearing()
    {
        _p2p.LocalScan.ClientsChanged -= OnClientsChanged;
        base.OnDisappearing();
    }

    private void OnClientsChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RefreshRows);
    }

    private void RefreshRows()
    {
        _rows.Clear();
        foreach (var p in _p2p.LocalScan.Clients)
        {
            var idShort = p.NetworkId.ToShortString();
            var online = p.TransportKind == TransportKind.MessengerServer
                ? p.MessengerServerOnline
                : _p2p.LocalScan.IsPeerSeenRecentlyOnLan(idShort) || p.MessengerServerOnline;
            _rows.Add(LanScanRow.From(p, online));
        }

        foreach (var mesh in _lora.ListDiscoveredPeers())
        {
            var id = ChatRepository.CanonicalPeerNetworkId(mesh.NetworkIdShort);
            if (id.Length == 0 || _rows.Any(r => ChatRepository.PeerNetworkIdsEqual(r.NetworkIdShort, id)))
                continue;
            var nick = string.IsNullOrWhiteSpace(mesh.Nickname) ? id : mesh.Nickname;
            try
            {
                var peer = new DiscoveredLocalPeer(
                    CompressedNetworkId.FromShortString(id),
                    nick,
                    ShortP2P.LoRa.MeshatasticTransportAddress.ToTransportAddress(
                        new ShortP2P.LoRa.MeshId(Convert.FromHexString(mesh.MeshIdHex))),
                    LoRaTransportKind.Meshtastic,
                    new DateTimeOffset(DateTime.SpecifyKind(mesh.LastSeenUtc, DateTimeKind.Utc)),
                    0);
                _rows.Add(LanScanRow.From(peer, _lora.HasRecentPeer(id)));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skip mesh peer {Id}", id);
            }
        }
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        ScanButton.IsEnabled = false;
        var sec = (int)Math.Round(LocalNetworkScanner.DefaultScanListenDuration.TotalSeconds);
        StatusLabel.Text = LocalizationUtils.GetStringByKeyWithFormat("lan.listening", sec);
        try
        {
            await Task.WhenAll(
                _p2p.LocalScan.ScanAsync(LocalNetworkScanner.DefaultScanListenDuration),
                _lora.ScanAsync()).ConfigureAwait(true);
            RefreshRows();
        }
        finally
        {
            StatusLabel.Text = "";
            ScanButton.IsEnabled = true;
        }
    }

    private async void OnPeerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Element el)
            return;
        var row = el.BindingContext as LanScanRow
                  ?? (el.Parent as Element)?.BindingContext as LanScanRow;
        if (row?.Peer == null)
        {
            if (row != null)
                await DisplayAlert(LocalizationUtils.GetStringByKey("lan.title"), row.DetailLine,
                    LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        try
        {
            await ChatNav.OpenDiscoveredPeerAsync(this, row.Peer).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LAN scan peer activation");
            await DisplayAlert(LocalizationUtils.GetStringByKey("lan.title"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
    }
}

