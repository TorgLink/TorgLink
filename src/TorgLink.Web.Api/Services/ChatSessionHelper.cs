using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using TorgLink.LoRa;

namespace TorgLink.Web.Api.Services;

internal static class ChatSessionHelper
{
    private static Task _shutdown = Task.CompletedTask;

    /// <summary>Next P2P start waits until a logout stop has finished.</summary>
    public static void TrackShutdown(Task shutdown)
    {
        _shutdown = Observe(shutdown);
    }

    public static async Task<ChatP2PSession?> EnsureSessionAsync(
        UserP2pRuntime p2p,
        AuthService auth,
        ChatRepository chats,
        ChatEntity chat,
        ILogger logger,
        MeshtasticTransportHost? lora = null,
        CancellationToken cancellationToken = default)
    {
        var user = auth.CurrentUser;
        if (user == null)
            return null;

        await WaitForShutdownAsync().ConfigureAwait(false);
        if (lora != null)
            await LoRaBootstrap.EnsureRunningAsync(lora, p2p, user, logger, cancellationToken).ConfigureAwait(false);
        else
            await p2p.EnsureStartedAsync(user, cancellationToken).ConfigureAwait(false);
        await MessengerServersBootstrap.EnsureRunningAsync(p2p, logger, cancellationToken).ConfigureAwait(false);
        if (!chat.IsBot)
        {
            await MessengerServersBootstrap.PublishChatRequestAsync(p2p, chat.PeerNetworkIdShort, logger, cancellationToken)
                .ConfigureAwait(false);
        }

        var session = p2p.GetSession(chat, user, auth, chats, null);
        if (!p2p.IsChatSessionStarted(chat.Id))
        {
            try
            {
                await session.StartAsync(cancellationToken).ConfigureAwait(false);
                p2p.MarkChatSessionStarted(chat.Id);
                AppLog.PeerConnected("chat-session", chat.PeerNetworkIdShort);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not start session for chat {ChatId}", chat.Id);
                throw;
            }
        }

        return session;
    }

    /// <summary>Attach session object without waiting for transport/handshake.</summary>
    public static ChatP2PSession? GetSessionOrNull(
        UserP2pRuntime p2p,
        AuthService auth,
        ChatRepository chats,
        ChatEntity chat)
    {
        var user = auth.CurrentUser;
        if (user == null)
            return null;
        return p2p.GetSession(chat, user, auth, chats, null);
    }

    /// <summary>Fire-and-forget P2P start so HTTP handlers (open chat / send text) stay responsive.</summary>
    public static void BeginEnsureSession(
        UserP2pRuntime p2p,
        AuthService auth,
        ChatRepository chats,
        ChatEntity chat,
        ILogger logger,
        MeshtasticTransportHost? lora = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await EnsureSessionAsync(p2p, auth, chats, chat, logger, lora).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background ensure session for chat {ChatId}", chat.Id);
            }
        });
    }

    public static async Task EnsureConnectivityAsync(
        UserP2pRuntime p2p,
        AuthService auth,
        ChatRepository chats,
        ILogger logger,
        MeshtasticTransportHost? lora = null,
        CancellationToken cancellationToken = default)
    {
        var user = auth.CurrentUser;
        if (user == null)
            return;
        await WaitForShutdownAsync().ConfigureAwait(false);
        if (lora != null)
            await LoRaBootstrap.EnsureRunningAsync(lora, p2p, user, logger, cancellationToken).ConfigureAwait(false);
        else
            await p2p.EnsureStartedAsync(user, cancellationToken).ConfigureAwait(false);
        AppLog.PeerConnected("p2p-runtime", user.NetworkIdShort);
        await MessengerServersBootstrap.EnsureRunningAsync(p2p, logger, cancellationToken).ConfigureAwait(false);
        await p2p.EnsureAllChatSessionsStartedAsync(user, auth, chats, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForShutdownAsync()
    {
        var pending = _shutdown;
        if (!pending.IsCompleted)
            await pending.ConfigureAwait(false);
    }

    private static async Task Observe(Task shutdown)
    {
        try
        {
            await shutdown.ConfigureAwait(false);
        }
        catch
        {
            // Logout already dropped the session; a failed stop must not block the next login.
        }
    }

    public static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        var s = parts[0];
        return s.Length >= 2
            ? $"{char.ToUpperInvariant(s[0])}{char.ToUpperInvariant(s[1])}"
            : char.ToUpperInvariant(s[0]).ToString();
    }

    public static string AvatarColor(string key)
    {
        string[] colors =
        [
            "#5B8DEF", "#E39B2B", "#34C759", "#AF52DE", "#FF6B6B", "#32ADE6", "#FF9500", "#5856D6"
        ];
        if (string.IsNullOrEmpty(key))
            return colors[0];
        var hash = 0;
        foreach (var c in key)
            hash = (hash * 31) + c;
        return colors[Math.Abs(hash) % colors.Length];
    }

    public static string Preview(ChatMessageEntity? m)
    {
        if (m == null)
            return "";
        if (m.PayloadKind == (int)ChatPayloadKind.Image)
            return "photo";
        if (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            return "voice";
        if (m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.TransferOffer)
            return string.IsNullOrWhiteSpace(m.TransferFileName)
                ? (string.IsNullOrWhiteSpace(m.Text) ? "file" : m.Text)
                : m.TransferFileName;
        return string.IsNullOrWhiteSpace(m.Text) ? "" : m.Text.Replace('\n', ' ');
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
