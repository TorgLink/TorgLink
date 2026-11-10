using System.Security.Claims;
using TorgLink.Web.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client;
using ShortP2P.Client.Bluetooth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Crypto;
using ShortP2P.Discovery;
using ShortP2P.Transport;
using ShortP2P.Transport.Abstractions;
using ShortP2P.TrustSystem;
using TorgLink.LoRa;

namespace TorgLink.Web.Api;

internal static class AppEndpoints
{
    public static void MapTorgLinkApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/bootstrap", Bootstrap);
        api.MapPost("/prefs/language", SetLanguage);
        api.MapPost("/prefs/theme", SetTheme);

        api.MapPost("/auth/login", Login);
        api.MapPost("/auth/register", Register);
        api.MapPost("/auth/logout", Logout);
        api.MapGet("/auth/me", Me);

        api.MapGet("/chats", ListChats);
        api.MapDelete("/chats/{id:int}", DeleteChat);
        api.MapPost("/chats/{id:int}/block", BlockChat);
        api.MapGet("/chats/{id:int}", GetChat);
        api.MapGet("/chats/{id:int}/commands", GetBotCommands);
        api.MapGet("/chats/{id:int}/messages", ListMessages);
        api.MapPost("/chats/{id:int}/messages", SendText);
        // Same-origin SPA + cookie auth (SameSite=Lax): form uploads don't send antiforgery tokens.
        api.MapPost("/chats/{id:int}/files", SendFile).DisableAntiforgery();
        api.MapGet("/chats/{id:int}/messages/{messageId:int}/file", DownloadFile);
        api.MapPost("/chats/{id:int}/messages/{messageId:int}/download", RequestDownload);
        api.MapPost("/chats/{id:int}/messages/{messageId:int}/retry", RetryMessage);
        api.MapPost("/chats/{id:int}/clear", ClearChat);
        api.MapPost("/chats/{id:int}/untrust", Untrust);

        api.MapPost("/chats/add", AddChat);
        api.MapPost("/chats/qr", ImportPeerQr).DisableAntiforgery();
        api.MapPost("/chats/open-peer", OpenDiscoveredPeer);

        api.MapGet("/contacts", ListContacts);
        api.MapPost("/contacts/scan", Scan);

        api.MapGet("/network", Network);
        api.MapGet("/network/qr", MyQr);
        api.MapGet("/network/addresses", Addresses);
        api.MapGet("/network/keys", Keys);

        api.MapGet("/servers", ListServers);
        api.MapPost("/servers", AddServer);
        api.MapPost("/servers/import-qr", ImportServerQr).DisableAntiforgery();
        api.MapPost("/servers/{id:int}/active", SetServerActive);
        api.MapPost("/servers/{id:int}/recheck", RecheckServer);
        api.MapPost("/servers/{id:int}/ask", AskServers);
        api.MapGet("/servers/{id:int}/qr", ServerQr);
        api.MapDelete("/servers/{id:int}", DeleteServer);

        api.MapGet("/settings", GetSettings);
        api.MapPost("/settings", SaveSettings);
        api.MapGet("/lora", GetLoRaSettings);
        api.MapGet("/lora/ports", ListLoRaPorts);
        api.MapPost("/lora", SaveLoRaSettings);
        api.MapGet("/lora/peers", ListLoRaPeers);
        api.MapGet("/lora/jurisdictions", ListLoRaJurisdictions);
        api.MapGet("/routing", GetRouting);
        api.MapPost("/routing", SaveRouting);

        api.MapGet("/blacklist", ListBlacklist);
        api.MapDelete("/blacklist/{networkId}", Unblock);

        api.MapGet("/logs", Logs);
        api.MapGet("/lan", Lan);
        api.MapPost("/lan/scan", LanScan);
    }

    private static IResult? NeedUser(AuthService auth) =>
        auth.CurrentUser == null ? Results.Unauthorized() : null;

    private static async Task SignInAsync(HttpContext http, int userId, string nickname)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, nickname)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity)).ConfigureAwait(false);
    }

    private static IResult Bootstrap(AuthService auth, UserP2pRuntime p2p)
    {
        var user = auth.CurrentUser;
        return Results.Json(new
        {
            language = WebPrefs.Get("torglink_language", "Russian"),
            languageChosen = WebPrefs.Get("torglink_language_chosen", false),
            theme = WebPrefs.Get("torglink_theme", "DarkFlame"),
            user = user == null ? null : UserDto(user, p2p)
        });
    }

    private static IResult SetLanguage(LanguageBody body)
    {
        WebPrefs.Set("torglink_language", body.Language ?? "Russian");
        WebPrefs.Set("torglink_language_chosen", true);
        AppLog.SettingChanged("Language", body.Language);
        return Results.Ok();
    }

    private static IResult SetTheme(ThemeBody body)
    {
        WebPrefs.Set("torglink_theme", body.Theme ?? "DarkFlame");
        AppLog.SettingChanged("Theme", body.Theme);
        return Results.Ok();
    }

    private static async Task<IResult> Login(LoginBody body, AuthService auth, UserP2pRuntime p2p,
        ChatRepository chats, MeshtasticTransportHost lora, ILoggerFactory logs, HttpContext http)
    {
        var (ok, err) = await auth.LoginAsync(body.Nickname ?? "", body.Password ?? "").ConfigureAwait(false);
        if (!ok)
            return Results.BadRequest(new { error = err ?? "login.failed" });
        var user = auth.CurrentUser!;
        await SignInAsync(http, user.Id, user.Nickname).ConfigureAwait(false);
        try
        {
            await ChatSessionHelper.EnsureConnectivityAsync(p2p, auth, chats, logs.CreateLogger("Login"), lora)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logs.CreateLogger("Login").LogWarning(ex, "Ensure P2P after login");
        }

        return Results.Json(UserDto(user, p2p));
    }

    private static async Task<IResult> Register(LoginBody body, AuthService auth, UserP2pRuntime p2p,
        ChatRepository chats, MeshtasticTransportHost lora, ILoggerFactory logs, HttpContext http)
    {
        if (!UserPasswordPolicy.TryValidate(body.Password, out var policyError))
            return Results.BadRequest(new { error = UserPasswordPolicy.Describe(policyError!.Value) });
        var (ok, err) = await auth.RegisterAsync(body.Nickname ?? "", body.Password ?? "").ConfigureAwait(false);
        if (!ok)
            return Results.BadRequest(new { error = err ?? "register.failed" });
        var user = auth.CurrentUser!;
        await SignInAsync(http, user.Id, user.Nickname).ConfigureAwait(false);
        try
        {
            await ChatSessionHelper.EnsureConnectivityAsync(p2p, auth, chats, logs.CreateLogger("Register"), lora)
                .ConfigureAwait(false);
        }
        catch
        {
            // same as MAUI: registration still succeeds
        }

        return Results.Json(UserDto(user, p2p));
    }

    private static async Task<IResult> Logout(AuthService auth, UserP2pRuntime p2p, MeshtasticTransportHost lora,
        HttpContext http)
    {
        await auth.LogoutAsync().ConfigureAwait(false);
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);

        // Do not hold the HTTP call on transport teardown. Long-poll can take the full
        // server timeout, and the settings button then never leaves the page.
        ChatSessionHelper.TrackShutdown(Task.Run(async () =>
        {
            try
            {
                await p2p.StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            try
            {
                await lora.StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }));
        return Results.Ok();
    }

    private static IResult Me(AuthService auth, UserP2pRuntime p2p)
    {
        var user = auth.CurrentUser;
        return user == null ? Results.Unauthorized() : Results.Json(UserDto(user, p2p));
    }

    private static object UserDto(ShortP2P.Auth.Data.UserEntity user, UserP2pRuntime p2p) => new
    {
        user.Id,
        user.Nickname,
        user.NetworkIdShort,
        user.DataUdpPort,
        initials = ChatSessionHelper.Initials(user.Nickname),
        avatar = ChatSessionHelper.AvatarColor(user.NetworkIdShort),
        meshOn = p2p.LocalScan.IsUdpListening || p2p.Settings.EnableUdpTransport,
        bluetoothOn = p2p.Settings.EnableBluetoothTransport && p2p.LocalScan.IsBluetoothListening
    };

    private static async Task<IResult> ListChats(AuthService auth, ChatRepository chats, UserP2pRuntime p2p,
        PeerBlacklist blacklist, MeshtasticTransportHost lora, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var u = auth.CurrentUser!;
        await blacklist.EnsureLoadedAsync(u.Id).ConfigureAwait(false);
        // Do not block the chats list on starting every P2P session / handshake.
        var chatsLog = logs.CreateLogger("Chats");
        _ = Task.Run(async () =>
        {
            try
            {
                await ChatSessionHelper.EnsureConnectivityAsync(p2p, auth, chats, chatsLog, lora)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                chatsLog.LogWarning(ex, "Ensure P2P");
            }
        });

        var list = await chats.ListChatsAsync(u.Id).ConfigureAwait(false);
        var rows = new List<object>();
        foreach (var c in list)
        {
            if (blacklist.IsBlocked(u.Id, c.PeerNetworkIdShort))
                continue;
            var lastPage = await chats.ListMessagesPageDescAsync(c.Id, 0, 1, includePayloadBlob: false)
                .ConfigureAwait(false);
            var last = lastPage.Count > 0 ? lastPage[0] : null;
            var ds = last == null ? "" : DeliveryGlyph(last);
            rows.Add(new
            {
                id = c.Id,
                nick = c.PeerNickname,
                networkId = c.PeerNetworkIdShort,
                initials = ChatSessionHelper.Initials(c.PeerNickname),
                avatar = ChatSessionHelper.AvatarColor(c.PeerNetworkIdShort),
                preview = ChatSessionHelper.Preview(last),
                time = last == null ? "" : ChatSessionHelper.TimeLabel(last.SentUtcTicks),
                online = p2p.LocalScan.IsPeerSeenRecentlyOnLan(c.PeerNetworkIdShort) || lora.HasRecentPeer(c.PeerNetworkIdShort),
                delivery = ds,
                isBot = c.IsBot
            });
        }

        return Results.Json(rows);
    }

    private static string DeliveryGlyph(ChatMessageEntity last)
    {
        if (!last.Outgoing)
            return "";
        var ds = (MessageDeliveryStatus)last.DeliveryStatus;
        if (ds == MessageDeliveryStatus.NotApplicable)
            ds = MessageDeliveryStatus.Delivered;
        return ds switch
        {
            MessageDeliveryStatus.Pending => "pending",
            MessageDeliveryStatus.Failed => "failed",
            MessageDeliveryStatus.Sent => "sent",
            _ => "ok"
        };
    }

    private static async Task<IResult> DeleteChat(int id, AuthService auth, ChatRepository chats, UserP2pRuntime p2p)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        await p2p.RemoveChatSessionAsync(id).ConfigureAwait(false);
        await chats.DeleteChatAsync(id, auth.CurrentUser!.Id).ConfigureAwait(false);
        return Results.Ok();
    }

    private static async Task<IResult> BlockChat(
        int id,
        AuthService auth,
        ChatRepository chats,
        PeerBlacklist blacklist,
        MessengerServerManager messengerServers)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null)
            return Results.NotFound();
        var u = auth.CurrentUser!;
        await blacklist.EnsureLoadedAsync(u.Id).ConfigureAwait(false);
        await blacklist.AddAsync(u.Id, chat.PeerNetworkIdShort, chat.PeerNickname).ConfigureAwait(false);
        if (chat.IsBot)
        {
            try
            {
                await BotHostClaims
                    .ClaimServersHostingBotAsync(messengerServers, chat.PeerNetworkIdShort)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Local block already applied; claims are best-effort.
            }
        }

        return Results.Ok();
    }

    private static async Task<IResult> GetChat(int id, AuthService auth, ChatRepository chats, UserP2pRuntime p2p,
        PeerBlacklist blacklist, MeshtasticTransportHost lora, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null)
            return Results.NotFound();
        var u = auth.CurrentUser!;
        await blacklist.EnsureLoadedAsync(u.Id).ConfigureAwait(false);
        if (blacklist.IsBlocked(u.Id, chat.PeerNetworkIdShort))
            return Results.Json(new { error = "blacklist.blocked" }, statusCode: 403);
        // Open chat UI immediately; transport/handshake continues in the background.
        ChatSessionHelper.BeginEnsureSession(p2p, auth, chats, chat, logs.CreateLogger("Chat"), lora);

        string safety;
        try
        {
            safety = PeerSafetyDisplay.FormatFingerprints(
                u.Nickname, auth.GetCurrentPublicKey(), chat.PeerNickname, chat.PeerRsaPublicJson);
        }
        catch
        {
            safety = "";
        }

        return Results.Json(new
        {
            id = chat.Id,
            nick = chat.PeerNickname,
            networkId = chat.PeerNetworkIdShort,
            initials = ChatSessionHelper.Initials(chat.PeerNickname),
            avatar = ChatSessionHelper.AvatarColor(chat.PeerNetworkIdShort),
            online = p2p.LocalScan.IsPeerSeenRecentlyOnLan(chat.PeerNetworkIdShort),
            safety,
            keySource = chat.PeerKeySourceKind,
            isBot = chat.IsBot
        });
    }

    private static async Task<IResult> GetBotCommands(int id, AuthService auth, ChatRepository chats,
        UserP2pRuntime p2p, PeerBlacklist blacklist, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null)
            return Results.NotFound();
        var u = auth.CurrentUser!;
        await blacklist.EnsureLoadedAsync(u.Id).ConfigureAwait(false);
        if (blacklist.IsBlocked(u.Id, chat.PeerNetworkIdShort))
            return Results.Json(new { error = "blacklist.blocked" }, statusCode: 403);
        if (!chat.IsBot)
            return Results.Json(new { error = "chat.commands_fail" }, statusCode: 400);

        if (p2p.MessengerServers == null)
            return Results.Json(new { error = "chat.commands_fail" }, statusCode: 503);

        try
        {
            await MessengerServersBootstrap.EnsureRunningAsync(p2p, logs.CreateLogger("Chat"), CancellationToken.None)
                .ConfigureAwait(false);
            var commands = await p2p.MessengerServers
                .FetchBotCommandsAsync(chat.PeerNetworkIdShort)
                .ConfigureAwait(false);
            return Results.Json(new
            {
                botNetworkId = chat.PeerNetworkIdShort,
                commands = commands.Select(c => new
                {
                    command = c.Command,
                    description = c.Description,
                    args = c.Args == null
                        ? Array.Empty<object>()
                        : c.Args.Select(a => new { argName = a.ArgName, argType = a.ArgType }).ToArray()
                }).ToArray()
            });
        }
        catch (Exception ex)
        {
            logs.CreateLogger("Chat").LogWarning(ex, "Bot commands for chat {ChatId}", id);
            return Results.Json(new { error = "chat.commands_fail" }, statusCode: 502);
        }
    }

    private static async Task<IResult> ListMessages(int id, AuthService auth, ChatRepository chats,
        PeerBlacklist blacklist, int offset = 0, int limit = 40)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null)
            return Results.NotFound();
        await blacklist.EnsureLoadedAsync(auth.CurrentUser!.Id).ConfigureAwait(false);
        if (blacklist.IsBlocked(auth.CurrentUser.Id, chat.PeerNetworkIdShort))
            return Results.Json(Array.Empty<object>());
        // Never pull ImageBlob into the list API — media is served via /messages/{id}/file.
        var pageDesc = await chats.ListMessagesPageDescAsync(id, offset, limit, includePayloadBlob: false)
            .ConfigureAwait(false);
        var chronological = pageDesc.Reverse().Select(MapMessage).ToList();
        return Results.Json(new { items = chronological, hasMore = pageDesc.Count == limit });
    }

    private static object MapMessage(ChatMessageEntity m)
    {
        var kind = "text";
        if (m.PayloadKind == (int)ChatPayloadKind.Image ||
            (m.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
            kind = "image";
        else if (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true ||
                 string.Equals(m.TransferPayloadKind, "voice", StringComparison.OrdinalIgnoreCase))
            kind = "voice";
        else if (m.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
            kind = "video";
        else if (m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.TransferOffer)
            kind = "file";
        var ds = (MessageDeliveryStatus)m.DeliveryStatus;
        return new
        {
            id = m.Id,
            outgoing = m.Outgoing,
            text = m.Text,
            kind,
            mime = m.MimeType,
            fileName = m.TransferFileName,
            size = m.TransferSizeBytes,
            time = ChatSessionHelper.TimeLabel(m.SentUtcTicks),
            delivery = DeliveryGlyph(m),
            transferState = m.TransferState,
            hasBlob = m.HasPayloadBlob || m.ImageBlob is { Length: > 0 }
        };
    }

    private static async Task<IResult> SendText(int id, TextBody body, AuthService auth, ChatRepository chats,
        UserP2pRuntime p2p, MeshtasticTransportHost lora, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null)
            return Results.NotFound();
        var logger = logs.CreateLogger("Send");
        try
        {
            // Persist + queue immediately; do not wait for UDP/BLE handshake (same as MAUI).
            var session = ChatSessionHelper.GetSessionOrNull(p2p, auth, chats, chat);
            if (session == null)
                return Results.Unauthorized();
            ChatSessionHelper.BeginEnsureSession(p2p, auth, chats, chat, logger, lora);
            await session.SendTextAsync(body.Text ?? "").ConfigureAwait(false);
            return Results.Ok();
        }
        catch (OutboundMessageQueuedException ex)
        {
            return Results.Json(new { queued = true, error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Send text");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> SendFile(int id, IFormFile file, AuthService auth, ChatRepository chats,
        UserP2pRuntime p2p, MeshtasticTransportHost lora, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null)
            return Results.NotFound();
        var mime = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        var limit = MediaFileLimits.LimitFor(p2p.Settings.TrafficQuality, mime);
        if (lora.IsRunning)
            limit = Math.Min(limit, MeshtasticTransportHost.MaxBinaryBytes);
        if (file.Length > limit)
            return Results.BadRequest(new { error = $"File is larger than {FormatByteLimit(limit)}." });

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms).ConfigureAwait(false);
        var bytes = ms.ToArray();
        if (bytes.Length > limit)
            return Results.BadRequest(new { error = $"File is larger than {FormatByteLimit(limit)}." });
        var logger = logs.CreateLogger("SendFile");
        AppLog.BinaryLoaded("chat-file", file.FileName, bytes.Length);
        try
        {
            var session = ChatSessionHelper.GetSessionOrNull(p2p, auth, chats, chat);
            if (session == null)
                return Results.Unauthorized();
            ChatSessionHelper.BeginEnsureSession(p2p, auth, chats, chat, logger, lora);
            if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                await session.SendImageAsync(bytes, mime).ConfigureAwait(false);
            else
                await session.SendFileAsync(file.FileName, bytes, mime).ConfigureAwait(false);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Send file");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DownloadFile(int id, int messageId, AuthService auth, ChatRepository chats)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var msg = await chats.GetMessageAsync(messageId).ConfigureAwait(false);
        if (msg == null || msg.ChatId != id || msg.ImageBlob is not { Length: > 0 } blob)
            return Results.NotFound();
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null || chat.UserId != auth.CurrentUser!.Id)
            return Results.NotFound();
        var mime = string.IsNullOrWhiteSpace(msg.MimeType) ? "application/octet-stream" : msg.MimeType;
        var name = string.IsNullOrWhiteSpace(msg.TransferFileName) ? "file" : msg.TransferFileName;
        // enableRangeProcessing lets <audio>/<video> seek without downloading the whole blob first.
        return Results.File(blob, mime, name, enableRangeProcessing: true);
    }

    private static async Task<IResult> RequestDownload(int id, int messageId, AuthService auth, ChatRepository chats,
        UserP2pRuntime p2p, MeshtasticTransportHost lora, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null)
            return Results.NotFound();
        var logger = logs.CreateLogger("Download");
        var session = await ChatSessionHelper.EnsureSessionAsync(p2p, auth, chats, chat, logger, lora)
            .ConfigureAwait(false);
        if (session == null)
            return Results.Unauthorized();
        await session.RequestBinaryDownloadAsync(messageId).ConfigureAwait(false);
        return Results.Ok();
    }

    private static async Task<IResult> RetryMessage(int id, int messageId, AuthService auth, ChatRepository chats,
        UserP2pRuntime p2p, MeshtasticTransportHost lora, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null)
            return Results.NotFound();
        var logger = logs.CreateLogger("Retry");
        var session = await ChatSessionHelper.EnsureSessionAsync(p2p, auth, chats, chat, logger, lora)
            .ConfigureAwait(false);
        if (session == null)
            return Results.Unauthorized();
        await session.RetryFailedMessageAsync(messageId).ConfigureAwait(false);
        return Results.Ok();
    }

    private static async Task<IResult> ClearChat(int id, AuthService auth, ChatRepository chats, UserP2pRuntime p2p,
        MeshtasticTransportHost lora, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        if (chat == null)
            return Results.NotFound();
        var logger = logs.CreateLogger("Clear");
        var session = await ChatSessionHelper.EnsureSessionAsync(p2p, auth, chats, chat, logger, lora)
            .ConfigureAwait(false);
        if (session != null)
            await session.ClearMessagesAsync().ConfigureAwait(false);
        else
            await chats.ClearMessagesAsync(id, auth.CurrentUser!.Id).ConfigureAwait(false);
        return Results.Ok();
    }

    private static async Task<IResult> Untrust(int id, AuthService auth, ChatRepository chats,
        MessengerServerManager servers, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var chat = await chats.GetChatAsync(id).ConfigureAwait(false);
        var hint = chat != null && PeerKeySource.IsServer(chat.PeerKeySourceKind)
            ? chat.PeerKeySourceDetail
            : null;
        try
        {
            await servers.MarkUntrustedWithFailoverAsync(null, hint).ConfigureAwait(false);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logs.CreateLogger("Untrust").LogWarning(ex, "Emergency untrust");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> AddChat(AddChatBody body, AuthService auth, ChatRepository chats,
        UserP2pRuntime p2p, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var nick = body.Nick?.Trim() ?? "";
        var id = body.NetworkId?.Trim() ?? "";
        var pub = body.PublicKey?.Trim() ?? "";
        var host = body.Host?.Trim() ?? "";
        if (nick.Length == 0 || id.Length == 0 || pub.Length == 0 || host.Length == 0)
            return Results.BadRequest(new { error = "addchat.fill_all" });
        if (body.Port is < 1 or > 65535)
            return Results.BadRequest(new { error = "addchat.bad_port" });
        try
        {
            _ = RsaKeySerializer.DeserializePublic(pub);
        }
        catch
        {
            return Results.BadRequest(new { error = "addchat.bad_key" });
        }

        var chat = await chats.AddChatAsync(auth.CurrentUser!.Id, nick, id, pub, host, body.Port,
            keySource: PeerKeySource.Manual()).ConfigureAwait(false);
        try
        {
            await p2p.TryEnsureChatSessionStartedAsync(chat.Id, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logs.CreateLogger("AddChat").LogWarning(ex, "Start session after add chat");
        }

        return Results.Json(new { id = chat.Id });
    }

    private static async Task<IResult> ImportPeerQr(IFormFile file, AuthService auth, ChatRepository chats,
        UserP2pRuntime p2p, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms).ConfigureAwait(false);
        var bytes = ms.ToArray();
        AppLog.BinaryLoaded("peer-qr", file.FileName, bytes.Length);
        if (!PeerQrService.TryDecodeImage(bytes, out var payload, out var err))
            return Results.BadRequest(new { error = err ?? "addchat.qr_fail" });
        var chat = await chats.AddChatAsync(auth.CurrentUser!.Id, payload.N, payload.Id, payload.K,
            payload.GetCommaSeparatedHosts(), payload.P, keySource: PeerKeySource.Qr()).ConfigureAwait(false);
        try
        {
            await p2p.TryEnsureChatSessionStartedAsync(chat.Id, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logs.CreateLogger("Qr").LogWarning(ex, "QR auto-install");
        }

        return Results.Json(new
        {
            id = chat.Id,
            nick = payload.N,
            networkId = payload.Id,
            publicKey = payload.K,
            host = payload.GetCommaSeparatedHosts(),
            port = payload.P
        });
    }

    private static async Task<IResult> OpenDiscoveredPeer(OpenPeerBody body, AuthService auth, ChatRepository chats,
        UserP2pRuntime p2p, PeerBlacklist blacklist, MeshtasticTransportHost lora)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        await blacklist.EnsureLoadedAsync(auth.CurrentUser!.Id).ConfigureAwait(false);
        if (blacklist.IsBlocked(auth.CurrentUser.Id, body.NetworkId))
            return Results.Json(new { error = "blacklist.blocked" }, statusCode: 403);
        var peer = p2p.LocalScan.Clients.FirstOrDefault(c =>
            ChatRepository.PeerNetworkIdsEqual(c.NetworkId.ToShortString(), body.NetworkId ?? ""));
        if (peer == null)
        {
            // Host-transport peer (e.g. LoRa/Meshtastic): start the chat the same way as LAN peers —
            // send a ChatInvite frame over the mesh transport and wait for the peer's reply invite.
            var mesh = lora.ListDiscoveredPeers().FirstOrDefault(p =>
                ChatRepository.PeerNetworkIdsEqual(p.NetworkIdShort, body.NetworkId ?? ""));
            if (mesh == null)
                return Results.NotFound();
            var idShort = ChatRepository.CanonicalPeerNetworkId(mesh.NetworkIdShort);
            try
            {
                var meshPeer = new DiscoveredLocalPeer(
                    CompressedNetworkId.FromShortString(idShort),
                    string.IsNullOrWhiteSpace(mesh.Nickname) ? idShort : mesh.Nickname,
                    ShortP2P.LoRa.MeshatasticTransportAddress.ToTransportAddress(
                        new ShortP2P.LoRa.MeshId(Convert.FromHexString(mesh.MeshIdHex))),
                    ShortP2P.LoRa.TransportKind.Meshtastic,
                    new DateTimeOffset(DateTime.SpecifyKind(mesh.LastSeenUtc, DateTimeKind.Utc)),
                    0);
                var meshResult = await LanChatStartFromDiscovery
                    .TryStartAsync(meshPeer, auth, chats, p2p.CreateLanChatStartContext()).ConfigureAwait(false);
                return meshResult.Kind switch
                {
                    LanChatStartKind.AlreadyExists or LanChatStartKind.Created when meshResult.Chat != null =>
                        Results.Json(new { id = meshResult.Chat.Id, kind = meshResult.Kind.ToString() }),
                    _ => Results.BadRequest(new { error = meshResult.Message ?? "network.error", kind = meshResult.Kind.ToString() })
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.BadRequest(new { error = ex.Message, kind = LanChatStartKind.Failed.ToString() });
            }
        }
        var result = await LanChatStartFromDiscovery
            .TryStartAsync(peer, auth, chats, p2p.CreateLanChatStartContext()).ConfigureAwait(false);
        return result.Kind switch
        {
            LanChatStartKind.AlreadyExists or LanChatStartKind.Created when result.Chat != null =>
                Results.Json(new { id = result.Chat.Id, kind = result.Kind.ToString() }),
            _ => Results.BadRequest(new { error = result.Message ?? "network.error", kind = result.Kind.ToString() })
        };
    }

    private static async Task<IResult> ListContacts(AuthService auth, ChatRepository chats, UserP2pRuntime p2p,
        MeshtasticTransportHost lora, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var u = auth.CurrentUser!;
        try
        {
            await ChatSessionHelper.EnsureConnectivityAsync(p2p, auth, chats, logs.CreateLogger("Contacts"), lora)
                .ConfigureAwait(false);
            await p2p.LocalScan.TriggerScanAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logs.CreateLogger("Contacts").LogWarning(ex, "Contacts probe");
        }

        IReadOnlyDictionary<string, string> versions = new Dictionary<string, string>(StringComparer.Ordinal);
        if (p2p.MessengerServers != null)
        {
            try
            {
                versions = await p2p.MessengerServers.DescribeBotVersionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logs.CreateLogger("Contacts").LogDebug(ex, "Bot versions");
            }
        }

        var rows = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in await chats.ListChatsAsync(u.Id).ConfigureAwait(false))
        {
            var id = ChatRepository.CanonicalPeerNetworkId(c.PeerNetworkIdShort);
            seen.Add(id);
            var peer = p2p.LocalScan.Clients.FirstOrDefault(x =>
                ChatRepository.PeerNetworkIdsEqual(x.NetworkId.ToShortString(), id));
            var isBot = c.IsBot || peer?.IsBot == true;
            var versionText = BotVersionText.Lookup(versions, id);
            rows.Add(new
            {
                chatId = (int?)c.Id,
                name = c.PeerNickname,
                detail = versionText.Length > 0 ? versionText : c.PeerNetworkIdShort,
                networkId = c.PeerNetworkIdShort,
                initials = ChatSessionHelper.Initials(c.PeerNickname),
                avatar = ChatSessionHelper.AvatarColor(c.PeerNetworkIdShort),
                online = p2p.LocalScan.IsPeerSeenRecentlyOnLan(c.PeerNetworkIdShort) || lora.HasRecentPeer(c.PeerNetworkIdShort),
                isBot
            });
        }

        foreach (var p in p2p.LocalScan.Clients)
        {
            var id = ChatRepository.CanonicalPeerNetworkId(p.NetworkId.ToShortString());
            if (id.Length == 0 || seen.Contains(id))
                continue;
            seen.Add(id);
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            var versionText = BotVersionText.Lookup(versions, id);
            rows.Add(new
            {
                chatId = (int?)null,
                name = nick,
                detail = versionText.Length > 0 ? versionText : $"{id} · {TransportLabel(p)}",
                networkId = id,
                initials = ChatSessionHelper.Initials(nick),
                avatar = ChatSessionHelper.AvatarColor(id),
                online = p.TransportKind == TransportKind.MessengerServer
                    ? p.MessengerServerOnline
                    : p2p.LocalScan.IsPeerSeenRecentlyOnLan(id) || p.MessengerServerOnline,
                isBot = p.IsBot
            });
        }

        AppendMeshContacts(rows, seen, lora);
        return Results.Json(rows);
    }

    private static void AppendMeshContacts(List<object> rows, HashSet<string> seen, MeshtasticTransportHost lora)
    {
        foreach (var p in lora.ListDiscoveredPeers())
        {
            var id = ChatRepository.CanonicalPeerNetworkId(p.NetworkIdShort);
            if (id.Length == 0 || !seen.Add(id))
                continue;
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            rows.Add(new
            {
                chatId = (int?)null,
                name = nick,
                detail = $"{id} · Meshtastic",
                networkId = id,
                initials = ChatSessionHelper.Initials(nick),
                avatar = ChatSessionHelper.AvatarColor(id),
                online = lora.HasRecentPeer(id),
                isBot = false
            });
        }
    }

    private static string TransportLabel(DiscoveredLocalPeer p) =>
        p.TransportKind switch
        {
            TransportKind.Udp => "LAN",
            TransportKind.Bluetooth => "Bluetooth",
            TransportKind.MessengerServer => "servers",
            ShortP2P.LoRa.TransportKind.Meshtastic => "Meshtastic",
            _ => p.TransportKind.ToString()
        };

    private static async Task<IResult> Scan(AuthService auth, UserP2pRuntime p2p, MeshtasticTransportHost lora)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        await Task.WhenAll(
            p2p.LocalScan.ScanAsync(LocalNetworkScanner.DefaultScanListenDuration),
            lora.ScanAsync()).ConfigureAwait(false);
        return Results.Ok();
    }

    private static IResult Network(AuthService auth, UserP2pRuntime p2p, MeshtasticTransportHost lora)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var nodes = new List<NetworkRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in p2p.LocalScan.Clients)
        {
            var id = p.NetworkId.ToShortString();
            seen.Add(ChatRepository.CanonicalPeerNetworkId(id));
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            var online = p.TransportKind == TransportKind.MessengerServer
                ? p.MessengerServerOnline
                : p2p.LocalScan.IsPeerSeenRecentlyOnLan(id) || p.MessengerServerOnline;
            nodes.Add(new NetworkRow(
                nick, id, ChatSessionHelper.Initials(nick), ChatSessionHelper.AvatarColor(id),
                online, TransportLabel(p)));
        }

        foreach (var p in lora.ListDiscoveredPeers())
        {
            var id = ChatRepository.CanonicalPeerNetworkId(p.NetworkIdShort);
            if (id.Length == 0 || !seen.Add(id))
                continue;
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            nodes.Add(new NetworkRow(
                nick, id, ChatSessionHelper.Initials(nick), ChatSessionHelper.AvatarColor(id),
                lora.HasRecentPeer(id), "Meshtastic"));
        }

        return Results.Json(nodes);
    }

    private static IResult MyQr(AuthService auth)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var u = auth.CurrentUser!;
        var pub = RsaKeySerializer.SerializePublic(auth.GetCurrentPublicKey());
        var png = PeerQrService.EncodeQrPng(PeerQrService.BuildPayload(u, pub));
        return Results.Json(new { png = Convert.ToBase64String(png) });
    }

    private static IResult Addresses(AuthService auth, UserP2pRuntime p2p)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var text = MyTransportEndpointsText.Build(auth.CurrentUser!, p2p.Settings, null);
        return Results.Json(new { text });
    }

    private static IResult Keys(AuthService auth)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var u = auth.CurrentUser!;
        var pub = RsaKeySerializer.SerializePublic(auth.GetCurrentPublicKey());
        var text = $"Network id: {u.NetworkIdShort}\nPublic key JSON:\n{pub}";
        return Results.Json(new { text });
    }

    private static async Task<IResult> ListServers(MessengerServerManager manager)
    {
        var servers = await manager.ListAsync().ConfigureAwait(false);
        var rows = servers.OrderByDescending(x => x.UpdatedUtcTicks).Select(s => new
        {
            s.Id,
            s.BaseUrl,
            s.Trusted,
            s.Active,
            s.IsRegistered,
            s.TrustRating,
            s.FingerprintSha256,
            isLowRating = s.TrustRating < TrustRatings.Floor,
            canAsk = s.TrustRating >= TrustRatings.Floor
        });
        return Results.Json(new { items = rows, max = MessengerServerLimits.MaxServersPerUser });
    }

    private static async Task<IResult> AddServer(ServerUrlBody body, MessengerServerManager manager)
    {
        var url = body.BaseUrl?.Trim() ?? "";
        if (url.Length == 0)
            return Results.BadRequest(new { error = "servers.need_url" });
        var entity = await manager.AddServerAsync(url).ConfigureAwait(false);
        AppLog.ServerResponse("AddServer", entity.BaseUrl, $"id={entity.Id}");
        return Results.Json(new { id = entity.Id, baseUrl = entity.BaseUrl });
    }

    private static async Task<IResult> ImportServerQr(IFormFile file, MessengerServerManager manager)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms).ConfigureAwait(false);
        var bytes = ms.ToArray();
        if (!MessengerServerQrService.TryDecodeImage(bytes, out var payload, out var err))
            return Results.BadRequest(new { error = err ?? "servers.import_read_fail" });
        var url = MessengerServerQrCodec.ToBaseUrl(payload);
        var existing = await manager.FindExistingByEndpointAsync(url).ConfigureAwait(false);
        if (existing != null)
            return Results.Json(new { already = true, baseUrl = existing.BaseUrl });
        var entity = await manager.AddServerAsync(url).ConfigureAwait(false);
        return Results.Json(new { id = entity.Id, baseUrl = entity.BaseUrl });
    }

    private static async Task<IResult> SetServerActive(int id, ActiveBody body, MessengerServerManager manager)
    {
        var list = await manager.ListAsync().ConfigureAwait(false);
        var row = list.FirstOrDefault(s => s.Id == id);
        if (row == null)
            return Results.NotFound();
        if (body.Active && !row.Trusted)
            return Results.BadRequest(new { error = "servers.untrusted_body" });
        await manager.SetActiveAsync(id, body.Active).ConfigureAwait(false);
        return Results.Ok();
    }

    private static async Task<IResult> RecheckServer(int id, MessengerServerManager manager)
    {
        var result = await manager.RecheckServerAsync(id).ConfigureAwait(false);
        return Results.Json(new
        {
            status = result.Status.ToString(),
            baseUrl = result.Server.BaseUrl,
            result.ErrorMessage,
            result.ExpectedFingerprint,
            result.ActualFingerprint
        });
    }

    private static async Task<IResult> AskServers(int id, MessengerServerManager manager)
    {
        var result = await manager.AskServersFromAsync(id).ConfigureAwait(false);
        return Results.Json(new
        {
            received = result.ReceivedCount,
            updated = result.UpdatedCount,
            added = result.AddedCount
        });
    }

    private static async Task<IResult> ServerQr(int id, MessengerServerManager manager)
    {
        var servers = await manager.ListAsync().ConfigureAwait(false);
        var row = servers.FirstOrDefault(s => s.Id == id);
        if (row == null)
            return Results.NotFound();
        if (!MessengerServerQrService.TryBuildPayload(row.BaseUrl, out var payload, out var err))
            return Results.BadRequest(new { error = err ?? "servers.share_fail" });
        var png = MessengerServerQrService.EncodeQrPng(payload);
        return Results.Json(new { png = Convert.ToBase64String(png), baseUrl = row.BaseUrl });
    }

    private static async Task<IResult> DeleteServer(int id, MessengerServerManager manager)
    {
        await manager.DeleteServerAsync(id).ConfigureAwait(false);
        return Results.Ok();
    }

    private static async Task<IResult> GetSettings(AuthService auth, UserP2pRuntime p2p, P2pRoutingSettingsStore store)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var persisted = await store.LoadAsync().ConfigureAwait(false);
        p2p.Settings.TrafficQuality = persisted.TrafficQuality;
        long bytes = 0;
        try
        {
            if (Directory.Exists(WebAppPaths.AppRoot))
                foreach (var f in Directory.EnumerateFiles(WebAppPaths.AppRoot, "*", SearchOption.AllDirectories))
                    bytes += new FileInfo(f).Length;
        }
        catch
        {
            // ignore
        }

        var u = auth.CurrentUser!;
        return Results.Json(new
        {
            nick = u.Nickname,
            networkId = u.NetworkIdShort,
            udpPort = u.DataUdpPort,
            initials = ChatSessionHelper.Initials(u.Nickname),
            avatar = ChatSessionHelper.AvatarColor(u.NetworkIdShort),
            bluetooth = p2p.Settings.EnableBluetoothTransport,
            lan = p2p.Settings.EnableUdpTransport,
            routing = p2p.Settings.AdvertisedPeerCapabilities.HasFlag(PresencePeerCapabilities.PeerSearch),
            trafficQuality = persisted.TrafficQuality.ToString(),
            language = WebPrefs.Get("torglink_language", "Russian"),
            theme = WebPrefs.Get("torglink_theme", "DarkFlame"),
            storageBytes = bytes
        });
    }

    private static async Task<IResult> SaveSettings(SettingsBody body, AuthService auth, UserP2pRuntime p2p,
        P2pRoutingSettingsStore store, IBluetoothTransportProvider bluetooth)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var s = await store.LoadAsync().ConfigureAwait(false);
        if (body.Bluetooth is { } bt)
            s.EnableBluetoothTransport = bt;
        if (body.Lan is { } lan)
            s.EnableUdpTransport = lan;
        if (body.Routing is { } routing)
        {
            var cap = (s.AdvertisedPeerCapabilities & ~PresencePeerCapabilities.PeerSearch) |
                      PresencePeerCapabilities.Chat;
            if (routing)
                cap |= PresencePeerCapabilities.PeerSearch;
            s.AdvertisedPeerCapabilities = cap;
        }

        if (!string.IsNullOrEmpty(body.TrafficQuality) &&
            Enum.TryParse<TrafficQualityMode>(body.TrafficQuality, out var tq))
            s.TrafficQuality = tq;
        await store.SaveAsync(s).ConfigureAwait(false);
        p2p.Settings.EnableUdpTransport = s.EnableUdpTransport;
        p2p.Settings.EnableBluetoothTransport = s.EnableBluetoothTransport;
        p2p.Settings.AdvertisedPeerCapabilities = s.AdvertisedPeerCapabilities | PresencePeerCapabilities.Chat;
        p2p.Settings.TrafficQuality = s.TrafficQuality;
        bluetooth.ApplySettings(s);
        return Results.Ok();
    }

    private static async Task<IResult> ListLoRaPorts(AuthService auth, MeshtasticTransportHost lora)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var selected = (await lora.LoadSettingsAsync().ConfigureAwait(false)).ComPort;
        return Results.Json(SerialComPorts.List(selected));
    }

    private static async Task<IResult> GetLoRaSettings(AuthService auth, MeshtasticTransportHost lora)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var s = await lora.LoadSettingsAsync().ConfigureAwait(false);
        return Results.Json(new
        {
            s.Enabled,
            s.ComPort,
            s.BaudRate,
            s.JurisdictionCode,
            s.TxPowerDbm,
            txPowerChoices = ShortP2P.LoRa.Jurisdiction.LoRaTxPower.UiChoices,
            running = lora.IsRunning,
            maxBinaryBytes = MeshtasticTransportHost.MaxBinaryBytes
        });
    }

    private static async Task<IResult> SaveLoRaSettings(LoRaBody body, AuthService auth, UserP2pRuntime p2p,
        MeshtasticTransportHost lora, ILoggerFactory logs)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var settings = new LoRaTransportSettings
        {
            Enabled = body.Enabled,
            ComPort = body.ComPort?.Trim() ?? "",
            BaudRate = body.BaudRate > 0 ? body.BaudRate : 115200,
            JurisdictionCode = string.IsNullOrWhiteSpace(body.JurisdictionCode)
                ? "UNSET"
                : body.JurisdictionCode.Trim(),
            TxPowerDbm = body.TxPowerDbm
        };
        await lora.SaveSettingsAsync(settings).ConfigureAwait(false);
        var user = auth.CurrentUser;
        var logger = logs.CreateLogger("LoRa");
        if (user != null && settings.Enabled)
        {
            try
            {
                await LoRaBootstrap.EnsureRunningAsync(lora, p2p, user, logger).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LoRa apply failed");
                return Results.BadRequest(new { error = ex.Message, running = lora.IsRunning });
            }
        }
        else
        {
            await lora.StopAsync().ConfigureAwait(false);
        }

        return Results.Json(new { ok = true, running = lora.IsRunning });
    }

    private static IResult ListLoRaPeers(AuthService auth, MeshtasticTransportHost lora)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var rows = new List<object>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in lora.ListDiscoveredPeers())
        {
            var id = ChatRepository.CanonicalPeerNetworkId(p.NetworkIdShort);
            if (id.Length == 0 || !seen.Add(id))
                continue;
            rows.Add(new
            {
                NetworkIdShort = p.NetworkIdShort,
                p.MeshIdHex,
                Subject = p.Nickname,
                IsActive = lora.HasRecentPeer(id),
                Confirmed = true,
                lastSeenUtc = p.LastSeenUtc
            });
        }

        foreach (var p in lora.ListPeers())
        {
            if (!seen.Add(ChatRepository.CanonicalPeerNetworkId(p.NetworkIdShort)))
                continue;
            rows.Add(new
            {
                p.NetworkIdShort,
                p.MeshIdHex,
                p.Subject,
                p.IsActive,
                p.Confirmed,
                lastSeenUtc = p.LastSeenUtc
            });
        }

        return Results.Json(rows);
    }

    private static IResult ListLoRaJurisdictions()
    {
        return Results.Json(ShortP2P.LoRa.Jurisdiction.JurisdictionCatalog.Regions
            .Select(r => new
            {
                r.Code,
                r.FrequencyMinMHz,
                r.FrequencyMaxMHz,
                r.DutyCycleMaxPercent,
                r.PowerMaxDbm,
                r.IsUnset
            }));
    }

    private static async Task<IResult> GetRouting(P2pRoutingSettingsStore store)
    {
        var s = await store.LoadAsync().ConfigureAwait(false);
        return Results.Json(new
        {
            s.MaxSearchHops,
            s.SendFailureSearchAttempts,
            delayMs = (int)s.SendFailureRetryDelay.TotalMilliseconds,
            timeoutMs = (int)s.SearchWaitTimeout.TotalMilliseconds,
            linkTechnology = s.LinkTechnology.ToString(),
            s.EnableUdpTransport,
            s.EnableBluetoothTransport,
            s.SuggestBluetoothPairing,
            advertisePeerSearch = s.AdvertisedPeerCapabilities.HasFlag(PresencePeerCapabilities.PeerSearch),
            presets = LinkTechnologyPresetExtensions.AllPresets
                .Select(p => new { value = p.ToString(), label = p.GetDisplayLabel() })
                .ToArray()
        });
    }

    private static async Task<IResult> SaveRouting(RoutingBody body, UserP2pRuntime runtime,
        P2pRoutingSettingsStore store, IBluetoothTransportProvider bluetooth)
    {
        if (body.MaxSearchHops is < 1 or > 3)
            return Results.BadRequest(new { error = "routing.err_depth" });
        if (body.SendFailureSearchAttempts < 1)
            return Results.BadRequest(new { error = "routing.err_attempts" });
        if (body.DelayMs < 0)
            return Results.BadRequest(new { error = "routing.err_delay" });
        if (body.TimeoutMs < 500)
            return Results.BadRequest(new { error = "routing.err_timeout" });
        var cap = (runtime.Settings.AdvertisedPeerCapabilities & ~PresencePeerCapabilities.PeerSearch) |
                  PresencePeerCapabilities.Chat;
        if (body.AdvertisePeerSearch)
            cap |= PresencePeerCapabilities.PeerSearch;
        var lt = LinkTechnologyPreset.Unlimited;
        if (!string.IsNullOrEmpty(body.LinkTechnology))
            Enum.TryParse(body.LinkTechnology, out lt);
        var settings = new P2pRoutingSettings
        {
            MaxSearchHops = body.MaxSearchHops,
            SendFailureSearchAttempts = body.SendFailureSearchAttempts,
            SendFailureRetryDelay = TimeSpan.FromMilliseconds(body.DelayMs),
            SearchWaitTimeout = TimeSpan.FromMilliseconds(body.TimeoutMs),
            LinkTechnology = lt,
            TrafficQuality = runtime.Settings.TrafficQuality,
            EnableUdpTransport = body.EnableUdpTransport,
            EnableBluetoothTransport = body.EnableBluetoothTransport,
            SuggestBluetoothPairing = body.SuggestBluetoothPairing,
            AdvertisedPeerCapabilities = cap
        };
        await store.SaveAsync(settings).ConfigureAwait(false);
        runtime.Settings.MaxSearchHops = settings.MaxSearchHops;
        runtime.Settings.SendFailureSearchAttempts = settings.SendFailureSearchAttempts;
        runtime.Settings.SendFailureRetryDelay = settings.SendFailureRetryDelay;
        runtime.Settings.SearchWaitTimeout = settings.SearchWaitTimeout;
        runtime.Settings.LinkTechnology = settings.LinkTechnology;
        runtime.Settings.EnableUdpTransport = settings.EnableUdpTransport;
        runtime.Settings.EnableBluetoothTransport = settings.EnableBluetoothTransport;
        runtime.Settings.SuggestBluetoothPairing = settings.SuggestBluetoothPairing;
        runtime.Settings.AdvertisedPeerCapabilities =
            settings.AdvertisedPeerCapabilities | PresencePeerCapabilities.Chat;
        bluetooth.ApplySettings(settings);
        return Results.Ok();
    }

    private static async Task<IResult> ListBlacklist(AuthService auth, PeerBlacklist blacklist)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var rows = await blacklist.ListAsync(auth.CurrentUser!.Id).ConfigureAwait(false);
        return Results.Json(rows.Select(r => new
        {
            r.NetworkId,
            nickname = string.IsNullOrWhiteSpace(r.Nickname) ? r.NetworkId : r.Nickname
        }));
    }

    private static async Task<IResult> Unblock(string networkId, AuthService auth, PeerBlacklist blacklist)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        await blacklist.RemoveAsync(auth.CurrentUser!.Id, networkId).ConfigureAwait(false);
        return Results.Ok();
    }

    private static IResult Logs()
    {
        var text = AppLogReader.ReadTodayLog(out var path);
        return Results.Json(new { path, text });
    }

    private static IResult Lan(AuthService auth, UserP2pRuntime p2p, MeshtasticTransportHost lora)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        var rows = new List<LanRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in p2p.LocalScan.Clients)
        {
            var id = p.NetworkId.ToShortString();
            seen.Add(ChatRepository.CanonicalPeerNetworkId(id));
            var online = p.TransportKind == TransportKind.MessengerServer
                ? p.MessengerServerOnline
                : p2p.LocalScan.IsPeerSeenRecentlyOnLan(id) || p.MessengerServerOnline;
            rows.Add(new LanRow(
                string.IsNullOrEmpty(p.Nickname) ? "—" : p.Nickname,
                id,
                online,
                TransportLabel(p),
                p.LastSeenUtc.ToLocalTime().ToString("g")));
        }

        foreach (var p in lora.ListDiscoveredPeers())
        {
            var id = ChatRepository.CanonicalPeerNetworkId(p.NetworkIdShort);
            if (id.Length == 0 || !seen.Add(id))
                continue;
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            rows.Add(new LanRow(nick, id, lora.HasRecentPeer(id), "Meshtastic",
                p.LastSeenUtc.ToLocalTime().ToString("g")));
        }

        return Results.Json(rows);
    }

    private static async Task<IResult> LanScan(AuthService auth, UserP2pRuntime p2p, MeshtasticTransportHost lora)
    {
        if (NeedUser(auth) is { } deny)
            return deny;
        await Task.WhenAll(
            p2p.LocalScan.ScanAsync(LocalNetworkScanner.DefaultScanListenDuration),
            lora.ScanAsync()).ConfigureAwait(false);
        return Results.Ok();
    }

    private static string FormatByteLimit(int bytes)
    {
        if (bytes >= 1024 * 1024 && bytes % (1024 * 1024) == 0)
            return $"{bytes / (1024 * 1024)} MB";
        return $"{(bytes + 1023) / 1024} KB";
    }

    private sealed record NetworkRow(
        string name, string networkId, string initials, string avatar, bool online, string hops);

    private sealed record LanRow(string nickname, string networkId, bool online, string transport, string lastSeen);

    private sealed record LoginBody(string? Nickname, string? Password);
    private sealed record LanguageBody(string? Language);
    private sealed record ThemeBody(string? Theme);
    private sealed record TextBody(string? Text);
    private sealed record AddChatBody(string? Nick, string? NetworkId, string? PublicKey, string? Host, int Port);
    private sealed record OpenPeerBody(string? NetworkId);
    private sealed record ServerUrlBody(string? BaseUrl);
    private sealed record ActiveBody(bool Active);
    private sealed record SettingsBody(bool? Bluetooth, bool? Lan, bool? Routing, string? TrafficQuality);
    private sealed record LoRaBody(bool Enabled, string? ComPort, int BaudRate, string? JurisdictionCode, int TxPowerDbm);
    private sealed record RoutingBody(
        int MaxSearchHops,
        int SendFailureSearchAttempts,
        int DelayMs,
        int TimeoutMs,
        string? LinkTechnology,
        bool EnableUdpTransport,
        bool EnableBluetoothTransport,
        bool SuggestBluetoothPairing,
        bool AdvertisePeerSearch);
}
