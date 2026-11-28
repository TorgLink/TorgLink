using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.MessengerServer.Http;
using ShortP2P.TrustSystem;

namespace TorgLink;

/// <summary>
/// TRL-27: after a local bot blacklist, file <c>ClaimServer(MALFUNCTIONED)</c>
/// about every ready messenger server that lists the bot (existing trust contract).
/// </summary>
internal static class BotHostClaims
{
    public static async Task<int> ClaimServersHostingBotAsync(
        MessengerServerManager manager,
        string botNetworkId,
        CancellationToken cancellationToken = default)
    {
        var id = ChatRepository.CanonicalPeerNetworkId(botNetworkId);
        if (id.Length == 0)
            return 0;

        var ready = await manager.EnsureAllActiveReadyAsync(cancellationToken).ConfigureAwait(false);
        var hosts = new List<MessengerServerConnection>();
        foreach (var conn in ready)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!manager.AllowsTraffic(conn) || !conn.HasValidToken)
                continue;

            IReadOnlyList<ShortP2P.MessengerServer.Contracts.Dtos.BotCatalogDto> list;
            try
            {
                list = await conn.Api.GetBotsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MessengerServerApiException ex) when ((int)ex.StatusCode == 404)
            {
                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                continue;
            }

            foreach (var bot in list)
            {
                if (!ChatRepository.PeerNetworkIdsEqual(bot.NetworkId, id))
                    continue;
                hosts.Add(conn);
                break;
            }
        }

        var claimed = 0;
        foreach (var host in hosts)
        {
            if (!TryParseEndpoint(host.Entity.BaseUrl, out var ip, out var port))
                continue;

            foreach (var peer in ready)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (peer.Entity.Id == host.Entity.Id)
                    continue;
                if (!manager.AllowsTraffic(peer) || !peer.HasValidToken)
                    continue;

                try
                {
                    await peer.Api
                        .ClaimServerAsync(ip, port, ServerClaimReason.MALFUNCTIONED, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Best-effort; local blacklist already applied by the caller.
                }
            }

            claimed++;
        }

        return claimed;
    }

    private static bool TryParseEndpoint(string? baseUrl, out string host, out int port)
    {
        host = "";
        port = 0;
        if (!Uri.TryCreate((baseUrl ?? "").Trim(), UriKind.Absolute, out var uri))
            return false;
#if NETCOREAPP
        host = uri.IdnHost;
        if (string.IsNullOrWhiteSpace(host))
            host = uri.Host;
#else
        host = uri.Host;
#endif
        port = uri.IsDefaultPort
            ? uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : uri.Port;
        return host.Length > 0 && port is >= 1 and <= 65535;
    }
}
