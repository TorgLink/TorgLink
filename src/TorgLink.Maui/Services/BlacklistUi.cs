using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using TorgLink.Localization;

namespace TorgLink.Maui.Services;

internal static class BlacklistUi
{
    public static async Task<bool> ConfirmAndBlockAsync(
        Page host,
        PeerBlacklist blacklist,
        int userId,
        string networkId,
        string nickname,
        bool isBot = false,
        MessengerServerManager? messengerServers = null)
    {
        var id = ChatRepository.CanonicalPeerNetworkId(networkId);
        if (userId <= 0 || id.Length == 0)
            return false;

        await blacklist.EnsureLoadedAsync(userId).ConfigureAwait(true);
        if (blacklist.IsBlocked(userId, id))
            return true;

        var label = string.IsNullOrWhiteSpace(nickname) ? id : nickname.Trim();
        var bodyKey = isBot ? "blacklist.add_body_bot" : "blacklist.add_body";
        var ok = await host.DisplayAlert(
            LocalizationUtils.GetStringByKey("blacklist.add_title"),
            LocalizationUtils.GetStringByKeyWithFormat(bodyKey, label),
            LocalizationUtils.GetStringByKey("blacklist.add"),
            LocalizationUtils.GetStringByKey("cancel")).ConfigureAwait(true);
        if (!ok)
            return false;

        await blacklist.AddAsync(userId, id, label).ConfigureAwait(true);
        if (isBot && messengerServers != null)
        {
            try
            {
                await BotHostClaims.ClaimServersHostingBotAsync(messengerServers, id).ConfigureAwait(true);
            }
            catch
            {
                // Local block already applied; claims are best-effort.
            }
        }

        return true;
    }
}
