using TorgLink.Web.Api;
using TorgLink.Web.Api.Hubs;
using TorgLink.Web.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Targets.Wrappers;
using ShortP2P.Auth;
using ShortP2P.Client;
using ShortP2P.Client.Bluetooth;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Client.Transport;
using ShortP2P.Crypto;
using ShortP2P.Discovery;
using ShortP2P.Discovery.Ble;
using ShortP2P.Discovery.Pings;
using ShortP2P.Discovery.Profile;
using ShortP2P.Discovery.RouteTables;
using ShortP2P.Transport;
using TorgLink.LoRa;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

SQLitePCL.Batteries_V2.Init();

var builder = WebApplication.CreateBuilder(args);
WebAppPaths.Initialize(builder.Environment.ContentRootPath);
WebPrefs.Initialize(WebAppPaths.PrefsPath);
ConfigureNLog();

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddNLog();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "torglink";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
// Frontend is served same-origin from wwwroot — no CORS needed.
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MediaFileLimits.MaxUploadBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MediaFileLimits.MaxUploadBytes);

builder.Services.AddSingleton(_ =>
{
    var opts = ChatMediaOptions.LoadOrDefault(WebAppPaths.ChatMediaPath);
    MediaFileLimits.ApplyStandardCeilings(opts);
    return opts;
});
builder.Services.AddSingleton(_ => new AppDatabase(WebAppPaths.DatabasePath));
builder.Services.AddSingleton<IUserAuthRepository, SqliteUserAuthRepository>();
builder.Services.AddRouteDbContextForAspNet(WebAppPaths.RoutesDbPath);
builder.Services.AddSingleton<ISessionStorage>(_ => new FileSessionStorage(WebAppPaths.SessionDirectory));
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ChatRepository>();
builder.Services.AddSingleton<PeerBlacklist>();
builder.Services.AddSingleton<IBluetoothPresencePingTargetsProvider, BluetoothPresencePingTargetsProvider>();
builder.Services.AddSingleton<IBleDiscoveredPeerStore, SqliteBleDiscoveredPeerStore>();
builder.Services.AddSingleton<IPeerProfileStore, SqlitePeerProfileStore>();
builder.Services.AddSingleton<ILocalPeerProfileSource, AuthLocalPeerProfileSource>();
builder.Services.AddSingleton<P2pRoutingSettingsStore>();
builder.Services.AddSingleton<IUdpTransportFactory, UdpTransportFactory>();
builder.Services.AddSingleton<ChatSessionCache>();
builder.Services.AddSingleton<P2pCryptoSessionCache>();
builder.Services.AddSingleton<IMessengerServerRepository, SqliteMessengerServerRepository>();
builder.Services.AddSingleton<IBotVersionRepository, SqliteBotVersionRepository>();
builder.Services.AddSingleton<DeviceIdProvider>();
builder.Services.AddSingleton<MessengerServerManager>();
builder.Services.AddSingleton<MessengerServerSyncService>();
builder.Services.AddSingleton<IBluetoothRadioCatalog, EmptyBluetoothRadioCatalog>();
builder.Services.AddSingleton<IBluetoothTransportProvider, NoOpBluetoothTransportProvider>();
builder.Services.AddSingleton<LoRaTransportSettingsStore>();
builder.Services.AddSingleton(sp => new MeshtasticTransportHost(
    sp.GetRequiredService<AuthService>(),
    sp.GetRequiredService<ChatRepository>(),
    sp.GetRequiredService<LoRaTransportSettingsStore>(),
    sp.GetRequiredService<ILoggerFactory>()));
builder.Services.AddSingleton<IAdditionalTransportProvider>(sp =>
    sp.GetRequiredService<MeshtasticTransportHost>());
builder.Services.AddSingleton(sp => new UserP2pRuntime(
    sp.GetRequiredService<P2pRoutingSettingsStore>(),
    sp.GetRequiredService<AuthService>(),
    sp.GetRequiredService<ChatRepository>(),
    sp.GetRequiredService<ChatMediaOptions>(),
    sp.GetRequiredService<IUdpTransportFactory>(),
    sp.GetRequiredService<ChatSessionCache>(),
    sp.GetRequiredService<P2pCryptoSessionCache>(),
    sp.GetRequiredService<IBluetoothTransportProvider>(),
    additionalDiscoveryTransports: null,
    sp.GetService<IRouteTableSnapshotSource>(),
    sp.GetService<IDiscoveryPingStore>(),
    blePeripheralScanner: null,
    sp.GetService<IBleDiscoveredPeerStore>(),
    sp.GetRequiredService<IBluetoothPresencePingTargetsProvider>(),
    sp.GetRequiredService<ILoggerFactory>(),
    sp.GetRequiredService<MessengerServerSyncService>(),
    sp.GetRequiredService<IPeerProfileStore>(),
    sp.GetRequiredService<ILocalPeerProfileSource>(),
    sp.GetRequiredService<IAdditionalTransportProvider>()));
builder.Services.AddHostedService<WebRuntimeHostedService>();
builder.Services.AddHostedService<RealtimeForwarder>();

var app = builder.Build();
var logFactory = app.Services.GetRequiredService<ILoggerFactory>();
AppLog.Initialize(logFactory);
P2PSession.TrafficLogger = logFactory.CreateLogger<P2PSession>();
logFactory.CreateLogger("TorgLink.Web.Api").LogInformation(
    "TorgLink web API started. Data directory: {Root}", WebAppPaths.AppRoot);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapTorgLinkApi();
app.MapHub<TorgLinkHub>("/hubs/torglink");
// SPA fallback: any non-API, non-hub GET serves the static frontend shell.
app.MapFallback(async ctx =>
{
    if (HttpMethods.IsGet(ctx.Request.Method))
    {
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.SendFileAsync(
            ctx.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider
                .GetFileInfo("index.html"));
        return;
    }
    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
});
app.Lifetime.ApplicationStopping.Register(() => LogManager.Shutdown());
app.Run();

static void ConfigureNLog()
{
    var logsDir = WebAppPaths.LogsDirectory;
    Directory.CreateDirectory(logsDir);
    var config = new LoggingConfiguration();
    var fileTarget = new FileTarget("gui-logfile")
    {
        FileName = Path.Combine(logsDir, "${date:format=dd.MM.yyyy}.log"),
        Layout =
            "${longdate}|${uppercase:${level}}|${logger}|${message}${onexception:inner=|${exception:format=tostring}}",
        Encoding = System.Text.Encoding.UTF8,
        KeepFileOpen = false,
        ConcurrentWrites = true,
        CreateDirs = true
    };
    var asyncTarget = new AsyncTargetWrapper(fileTarget, 1000, AsyncTargetWrapperOverflowAction.Discard);
    config.AddTarget(asyncTarget);
    config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, asyncTarget);
    LogManager.Configuration = config;
}

sealed class WebRuntimeHostedService(
    IServiceProvider services,
    AuthService auth,
    UserP2pRuntime p2p,
    MeshtasticTransportHost lora,
    ILogger<WebRuntimeHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await services.ApplyRouteDatabaseMigrationsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Route database migration failed");
        }

        try
        {
            await auth.TryRestoreSessionAsync().ConfigureAwait(false);
            if (auth.CurrentUser != null)
            {
                await LoRaBootstrap.EnsureRunningAsync(lora, p2p, auth.CurrentUser, logger, cancellationToken)
                    .ConfigureAwait(false);
                await MessengerServersBootstrap.EnsureRunningAsync(p2p, logger, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "P2P runtime start failed");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await p2p.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "P2P runtime stop failed");
        }

        try
        {
            await lora.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LoRa transport stop failed");
        }
    }
}
