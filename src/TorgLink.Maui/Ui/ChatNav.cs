using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using TorgLink.Maui.Services;
using TorgLink.Localization;

namespace TorgLink.Maui;

internal static class ChatNav
{
    private static int _opening;

    public static async Task OpenChatAsync(Page host, int chatId)
    {
        if (Interlocked.CompareExchange(ref _opening, 1, 0) != 0)
            return;

        try
        {
            var nav = Shell.Current?.Navigation ?? host.Navigation;
            if (nav.NavigationStack.Count > 0 &&
                nav.NavigationStack[^1] is ChatDetailPage already &&
                already.ChatId == chatId)
                return;

            // Push immediately: blacklist/DB checks on the detail page. Waiting here
            // (SQLite on the UI thread) is what made taps miss and WinUI cancel navigation.
            var page = MauiProgram.Services.GetRequiredService<ChatDetailPage>();
            page.ChatId = chatId;
            await nav.PushAsync(page).ConfigureAwait(true);
        }
        finally
        {
            Interlocked.Exchange(ref _opening, 0);
        }
    }

    public static async Task OpenDiscoveredPeerAsync(Page host, DiscoveredLocalPeer peer)
    {
        var auth = MauiProgram.Services.GetRequiredService<AuthService>();
        var chats = MauiProgram.Services.GetRequiredService<ChatRepository>();
        var p2p = MauiProgram.Services.GetRequiredService<UserP2pRuntime>();
        var blacklist = MauiProgram.Services.GetRequiredService<PeerBlacklist>();
        var user = auth.CurrentUser;
        if (user != null)
        {
            await blacklist.EnsureLoadedAsync(user.Id).ConfigureAwait(true);
            if (blacklist.IsBlocked(user.Id, peer.NetworkId.ToShortString()))
            {
                await host.DisplayAlert(LocalizationUtils.GetStringByKey("blacklist.title"), LocalizationUtils.GetStringByKey("blacklist.blocked"), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return;
            }
        }

        var result = await LanChatStartFromDiscovery
            .TryStartAsync(peer, auth, chats, p2p.CreateLanChatStartContext(), CancellationToken.None).ConfigureAwait(true);
        switch (result.Kind)
        {
            case LanChatStartKind.AlreadyExists:
            case LanChatStartKind.Created:
                if (result.Chat != null)
                    await OpenChatAsync(host, result.Chat.Id).ConfigureAwait(true);
                break;
            case LanChatStartKind.WaitingForPeer:
                await host.DisplayAlert(LocalizationUtils.GetStringByKey("network.title"), result.Message ?? "", LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
                break;
            case LanChatStartKind.Failed:
                await host.DisplayAlert(LocalizationUtils.GetStringByKey("network.title"), result.Message ?? LocalizationUtils.GetStringByKey("network.error"), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                break;
        }
    }

    public static string Preview(ChatMessageEntity? m)
    {
        if (m == null)
            return LocalizationUtils.GetStringByKey("preview.none");
        if (m.PayloadKind == (int)ChatPayloadKind.Image)
            return LocalizationUtils.GetStringByKey("preview.photo");
        if (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            return LocalizationUtils.GetStringByKey("preview.voice");
        if (m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.TransferOffer)
            return string.IsNullOrWhiteSpace(m.TransferFileName)
                ? (string.IsNullOrWhiteSpace(m.Text) ? LocalizationUtils.GetStringByKey("preview.file") : m.Text)
                : m.TransferFileName;
        return string.IsNullOrWhiteSpace(m.Text) ? LocalizationUtils.GetStringByKey("preview.message") : m.Text.Replace('\n', ' ');
    }

    public static string TimeLabel(long utcTicks)
    {
        if (utcTicks <= 0)
            return "";
        var local = new DateTimeOffset(utcTicks, TimeSpan.Zero).ToLocalTime();
        return local.Date == DateTime.Today
            ? local.ToString("HH:mm")
            : local.ToString("dd.MM");
    }
}

