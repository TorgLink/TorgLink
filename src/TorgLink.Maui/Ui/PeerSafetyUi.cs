using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Crypto;
using TorgLink.Localization;

namespace TorgLink.Maui;

internal static class PeerSafetyUi
{
    public static string OwnHeader(AuthService auth, UserEntity user)
    {
        try
        {
            return $"{user.Nickname}: {SafetyNumber.FromPublicKey(auth.GetCurrentPublicKey())}";
        }
        catch
        {
            return "";
        }
    }

    public static string ChatPanel(AuthService auth, ChatEntity chat, ChatP2PSession? session = null)
    {
        var user = auth.CurrentUser;
        if (user == null)
            return "";

        try
        {
            var fingerprints = PeerSafetyDisplay.FormatFingerprints(
                user.Nickname, auth.GetCurrentPublicKey(), chat.PeerNickname, chat.PeerRsaPublicJson);
            var channel = LocalizedChannel(chat, session);
            return channel.Length == 0
                ? fingerprints
                : fingerprints + Environment.NewLine + LocalizationUtils.GetStringByKeyWithFormat("safety.channel", channel);
        }
        catch
        {
            return "";
        }
    }

    public static string LocalizedChannel(ChatEntity chat, ChatP2PSession? session = null)
    {
        if (session != null && session.IsCommunicatingViaMessengerServer)
        {
            var endpoint = PeerSafetyDisplay.FormatServerEndpoint(session.ActiveMessengerServerBaseUrl ?? "");
            return endpoint.Length == 0
                ? LocalizationUtils.GetStringByKey("safety.ch_server")
                : LocalizationUtils.GetStringByKeyWithFormat("safety.ch_server_ep", endpoint);
        }

        var kind = (chat.PeerKeySourceKind ?? "").Trim();
        if (kind.Length == 0)
            return "";
        if (string.Equals(kind, PeerKeySourceKinds.Udp, StringComparison.OrdinalIgnoreCase))
            return LocalizationUtils.GetStringByKey("safety.ch_udp");
        if (string.Equals(kind, PeerKeySourceKinds.Bluetooth, StringComparison.OrdinalIgnoreCase))
            return LocalizationUtils.GetStringByKey("safety.ch_bt");
        if (PeerKeySource.IsServer(kind))
        {
            var endpoint = PeerSafetyDisplay.FormatServerEndpoint(chat.PeerKeySourceDetail ?? "");
            return endpoint.Length == 0
                ? LocalizationUtils.GetStringByKey("safety.ch_server")
                : LocalizationUtils.GetStringByKeyWithFormat("safety.ch_server_ep", endpoint);
        }

        if (string.Equals(kind, PeerKeySourceKinds.Qr, StringComparison.OrdinalIgnoreCase))
            return LocalizationUtils.GetStringByKey("safety.ch_qr");
        if (string.Equals(kind, PeerKeySourceKinds.Manual, StringComparison.OrdinalIgnoreCase))
            return LocalizationUtils.GetStringByKey("safety.ch_manual");
        return kind;
    }

    public static async Task MarkUntrustedAsync(
        MessengerServerManager servers,
        ChatEntity? chat,
        ILogger logger,
        Page page)
    {
        var hint = chat != null && PeerKeySource.IsServer(chat.PeerKeySourceKind)
            ? chat.PeerKeySourceDetail
            : null;
        try
        {
            await servers.MarkUntrustedWithFailoverAsync(null, hint).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Emergency untrust failed");
            await page.DisplayAlert(LocalizationUtils.GetStringByKey("safety.emergency"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
    }
}

