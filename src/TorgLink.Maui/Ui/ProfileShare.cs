using ShortP2P.Auth;
using ShortP2P.Client;
using ShortP2P.Client.Bluetooth;
using ShortP2P.Client.Services;
using ShortP2P.Crypto;
using ShortP2P.Transport;
using TorgLink.Localization;

namespace TorgLink.Maui;

internal static class ProfileShare
{
    public static async Task CopyAddressesAsync(Page host, AuthService auth, UserP2pRuntime p2p,
        IBluetoothRadioCatalog bluetoothCatalog)
    {
        var u = auth.CurrentUser;
        if (u == null)
            return;
        string? bt = null;
        try
        {
            bt = await BluetoothRoutingMac.GetEffectiveMacAsync(p2p.Settings, bluetoothCatalog)
                .ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }

        // NIC enum + public IP lookup are sync/blocking — keep off UI thread.
        var text = await Task.Run(() => MyTransportEndpointsText.Build(u, p2p.Settings, bt))
            .ConfigureAwait(true);
        await Clipboard.Default.SetTextAsync(text).ConfigureAwait(true);
        await host.DisplayAlert(LocalizationUtils.GetStringByKey("copied"), LocalizationUtils.GetStringByKey("copied.addresses"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
    }

    public static async Task CopyKeysAsync(Page host, AuthService auth)
    {
        var u = auth.CurrentUser;
        if (u == null)
            return;
        var pub = IdentityKeySerializer.SerializePublic(auth.GetCurrentPublicKey());
        var text = $"Network id: {u.NetworkIdShort}\nPublic key JSON:\n{pub}";
        await Clipboard.Default.SetTextAsync(text).ConfigureAwait(true);
        await host.DisplayAlert(LocalizationUtils.GetStringByKey("copied"), LocalizationUtils.GetStringByKey("copied.keys"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
    }
}

