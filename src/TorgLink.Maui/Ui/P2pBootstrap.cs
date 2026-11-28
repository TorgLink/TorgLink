using Microsoft.Extensions.Logging;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Services;
using TorgLink.LoRa;

namespace TorgLink.Maui;

/// <summary>Starts LoRa (when configured) then <see cref="UserP2pRuntime"/>.</summary>
internal static class P2pBootstrap
{
    private static Task _shutdown = Task.CompletedTask;

    /// <summary>Next start waits until logout has finished tearing transports down.</summary>
    public static void TrackShutdown(Task shutdown)
    {
        _shutdown = Observe(shutdown);
    }

    public static async Task EnsureStartedAsync(
        UserP2pRuntime p2p,
        UserEntity user,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var pending = _shutdown;
        if (!pending.IsCompleted)
            await pending.ConfigureAwait(false);

        var host = MauiProgram.Services.GetService<MeshtasticTransportHost>();
        if (host == null)
            await p2p.EnsureStartedAsync(user, cancellationToken).ConfigureAwait(false);
        else
            await LoRaBootstrap.EnsureRunningAsync(host, p2p, user, logger, cancellationToken).ConfigureAwait(false);
    }

    private static async Task Observe(Task shutdown)
    {
        try
        {
            await shutdown.ConfigureAwait(false);
        }
        catch
        {
            // The logout caller already logged the failure.
        }
    }
}
