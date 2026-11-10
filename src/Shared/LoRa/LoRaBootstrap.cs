using Microsoft.Extensions.Logging;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Services;

namespace TorgLink.LoRa;

/// <summary>Start LoRa host before (or with) <see cref="UserP2pRuntime.EnsureStartedAsync"/>.</summary>
public static class LoRaBootstrap
{
    public static async Task EnsureRunningAsync(
        MeshtasticTransportHost host,
        UserP2pRuntime p2p,
        UserEntity user,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(p2p);
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            await host.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "LoRa / Meshtastic transport failed to start");
        }

        await p2p.EnsureStartedAsync(user, cancellationToken).ConfigureAwait(false);
    }
}
