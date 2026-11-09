using TorgLink.Localization;
using TorgLink.Maui.Localization; // MauiLanguageBootstrap
using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Discovery.RouteTables;

namespace TorgLink.Maui;

public partial class App : Application
{
    private int _permissionsBootstrapped;
    private int _deferredStart;

    public App()
    {
        var boot = NLog.LogManager.GetLogger("AppBoot");
        try
        {
            boot.Info("App ctor: InitializeComponent");
            InitializeComponent();
            boot.Info("App ctor: MauiLanguageBootstrap");
            MauiLanguageBootstrap.Initialize();
            boot.Info("App ctor: VocabularyBootstrap");
            try
            {
                VocabularyBootstrap.InitializeAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                boot.Error(ex, "VocabularyBootstrap failed");
            }
            boot.Info("App ctor: ThemeService");
            ThemeService.LoadAndApply();
            boot.Info("App ctor: UiInteractionLog");
            UiInteractionLog.HookApplication(this);

            boot.Info("App ctor: creating main page");
            MainPage = LanguageService.HasChosen
                ? new NavigationPage(MauiProgram.Services.GetRequiredService<LoginPage>())
                : new NavigationPage(new LanguageSelectPage());
            boot.Info("App ctor: main page ready");
        }
        catch (Exception ex)
        {
            boot.Error(ex, "App ctor failed");
            NLog.LogManager.Flush();
            throw;
        }
    }

    protected override void OnStart()
    {
        base.OnStart();

        var logger = MauiProgram.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Application started");
        if (Interlocked.Exchange(ref _permissionsBootstrapped, 1) == 0)
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await AppPermissionsBootstrapper.EnsureRequestedAsync(logger).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Permission bootstrap failed");
                }
            });
        if (Interlocked.Exchange(ref _deferredStart, 1) == 0)
            MainThread.BeginInvokeOnMainThread(() => _ = StartBackgroundServicesAsync(logger));
    }

    private static async Task StartBackgroundServicesAsync(ILogger logger)
    {
        try
        {
            await MauiProgram.Services.ApplyRouteDatabaseMigrationsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Route database migration failed");
        }

        try
        {
            var p2p = MauiProgram.Services.GetRequiredService<UserP2pRuntime>();
            p2p.LocalScan.ClientsChanged += (_, _) =>
                AppLog.Network.LogInformation("Peer directory changed (LAN/server scan)");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "P2P runtime start failed");
        }

        await ApplyBluetoothSettingsAsync(logger).ConfigureAwait(false);
    }

    private static async Task ApplyBluetoothSettingsAsync(ILogger logger)
    {
        try
        {
            var store = MauiProgram.Services.GetRequiredService<P2pRoutingSettingsStore>();
            var routing = await store.LoadAsync().ConfigureAwait(false);
            await Task.Run(() =>
                MauiProgram.Services.GetRequiredService<MauiBluetoothTransportRegistration>().ApplySettings(routing)
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Deferred Bluetooth settings apply failed");
        }
    }
}
