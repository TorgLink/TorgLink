using ShortP2P.Discovery;

namespace TorgLink.WinForms;

internal static class RoutingSettingsLive
{
    /// <summary>Copy persisted values onto the live singleton (scanner holds this instance).</summary>
    public static void Overlay(P2pRoutingSettings dest, P2pRoutingSettings src)
    {
        dest.MaxSearchHops = src.MaxSearchHops;
        dest.SendFailureSearchAttempts = src.SendFailureSearchAttempts;
        dest.SendFailureRetryDelay = src.SendFailureRetryDelay;
        dest.SearchWaitTimeout = src.SearchWaitTimeout;
        dest.LinkTechnology = src.LinkTechnology;
        dest.EnableUdpTransport = src.EnableUdpTransport;
        dest.EnableBluetoothTransport = false;
        dest.SelectedBluetoothAdapterDeviceId = null;
        dest.SelectedBluetoothAdapterMac = null;
        dest.SuggestBluetoothPairing = false;
        dest.TrafficQuality = src.TrafficQuality;
        dest.AdvertisedPeerCapabilities = (src.AdvertisedPeerCapabilities | PresencePeerCapabilities.Chat)
                                        & PresencePeerCapabilities.AllDefined;
        dest.VisibleForSearch = src.VisibleForSearch;
    }
}
