using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.Logging;
using TorgLink.Maui.Services;
using ShortP2P.Auth;
using ShortP2P.Client.Bluetooth;
using ShortP2P.Client.Data.Abstractions;
using ShortP2P.Client.ProfileBackup;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using ShortP2P.LoRa.Jurisdiction;
using TorgLink.Localization;
using TorgLink.LoRa;

namespace TorgLink.Maui;

public partial class SettingsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly IBluetoothTransportProvider _bluetoothTransport;
    private readonly ILogger<SettingsPage> _logger;
    private readonly UserP2pRuntime _p2p;
    private readonly P2pRoutingSettingsStore _store;
    private readonly DatabaseProviderSettings _databaseSettings;
    private readonly ProfileBackupService _profileBackup;
    private readonly MeshtasticTransportHost _lora;
    private bool _suppressToggle;

    public SettingsPage(AuthService auth, UserP2pRuntime p2p, P2pRoutingSettingsStore store,
        IBluetoothTransportProvider bluetoothTransport, DatabaseProviderSettings databaseSettings, ILogger<SettingsPage> logger,
        ProfileBackupService profileBackup, MeshtasticTransportHost lora)
    {
        InitializeComponent();
        _auth = auth;
        _p2p = p2p;
        _store = store;
        _bluetoothTransport = bluetoothTransport;
        _databaseSettings = databaseSettings;
        _logger = logger;
        _profileBackup = profileBackup;
        _lora = lora;
        LanguageService.Changed += OnLanguageChanged;
        ProfileRow.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<ProfilePage>())
                    .ConfigureAwait(true);
            })
        });
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync().ConfigureAwait(true));

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync().ConfigureAwait(true);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task LoadAsync()
    {
        ApplyLocalizedChrome();
        var u = _auth.CurrentUser;
        Header.Bind(u, _p2p);
        if (u != null)
        {
            ProfileName.Text = u.Nickname;
            ProfileId.Text = u.NetworkIdShort;
            AvatarBadge.Apply(ProfileAvatar, ProfileInitials, ProfileAvatarImage, u.Nickname, u.NetworkIdShort,
                u.Avatar);
            UdpPortLabel.Text = u.DataUdpPort.ToString();
        }

        var persisted = await _store.LoadAsync().ConfigureAwait(true);
        MediaEconomy.Apply(_p2p, persisted.TrafficQuality);

        _suppressToggle = true;
        BluetoothSwitch.IsToggled = _p2p.Settings.EnableBluetoothTransport;
        LanSwitch.IsToggled = _p2p.Settings.EnableUdpTransport;
        RoutingSwitch.IsToggled = _p2p.Settings.AdvertisedPeerCapabilities.HasFlag(PresencePeerCapabilities.PeerSearch);
        _suppressToggle = false;
        BluetoothHint.Text = BluetoothSwitch.IsToggled ? LocalizationUtils.GetStringByKey("on") : LocalizationUtils.GetStringByKey("off");
        LanHint.Text = LanSwitch.IsToggled ? LocalizationUtils.GetStringByKey("on") : LocalizationUtils.GetStringByKey("off");
        RoutingHint.Text = RoutingSwitch.IsToggled ? LocalizationUtils.GetStringByKey("on") : LocalizationUtils.GetStringByKey("off");
        EconomyHint.Text = MediaEconomy.Hint(_p2p.Settings.TrafficQuality);
        StorageLabel.Text = FormatStorage();

        // Initialize database provider picker
        InitializeDatabaseProviderPicker();
        await LoadLoRaSettingsAsync().ConfigureAwait(true);

        RebuildLanguageTiles();
        RebuildThemeChips();
        RebuildEconomyChips();
        await Task.CompletedTask.ConfigureAwait(true);
    }

    private async Task LoadLoRaSettingsAsync()
    {
        var s = await _lora.LoadSettingsAsync().ConfigureAwait(true);
        _suppressToggle = true;
        LoRaSwitch.IsToggled = s.Enabled;
        var ports = SerialComPorts.List(s.ComPort).ToList();
        LoRaComPortPicker.ItemsSource = ports;
        var portIndex = ports.FindIndex(p => string.Equals(p, s.ComPort, StringComparison.OrdinalIgnoreCase));
        LoRaComPortPicker.SelectedIndex = portIndex >= 0 ? portIndex : 0;
        var codes = JurisdictionCatalog.Regions.Select(r => r.Code).ToList();
        LoRaJurisdictionPicker.ItemsSource = codes;
        var idx = codes.FindIndex(c => string.Equals(c, s.JurisdictionCode, StringComparison.OrdinalIgnoreCase));
        LoRaJurisdictionPicker.SelectedIndex = idx >= 0 ? idx : 0;
        _suppressToggle = false;
        LoRaStatusLabel.Text = _lora.IsRunning
            ? "LoRa transport: running"
            : s.Enabled
                ? "LoRa transport: not running (check COM / jurisdiction)"
                : "LoRa transport: off";
    }

    private void OnLoRaToggled(object? sender, ToggledEventArgs e)
    {
        // Applied on Save.
    }

    private async void OnLoRaSaveClicked(object? sender, EventArgs e)
    {
        var code = LoRaJurisdictionPicker.SelectedItem as string ?? JurisdictionCatalog.UnsetCode;
        var settings = new LoRaTransportSettings
        {
            Enabled = LoRaSwitch.IsToggled,
            ComPort = LoRaComPortPicker.SelectedItem as string ?? "COM1",
            BaudRate = 115200,
            JurisdictionCode = code
        };
        try
        {
            await _lora.SaveSettingsAsync(settings).ConfigureAwait(true);
            var user = _auth.CurrentUser;
            if (user != null)
                await LoRaBootstrap.EnsureRunningAsync(_lora, _p2p, user, _logger).ConfigureAwait(true);
            else if (!settings.Enabled)
                await _lora.StopAsync().ConfigureAwait(true);
            await LoadLoRaSettingsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Apply LoRa settings failed");
            LoRaStatusLabel.Text = ex.Message;
        }
    }

    private void InitializeDatabaseProviderPicker()
    {
        DatabasePicker.ItemsSource = DatabaseProviderSettings.AvailableProviders
            .Select(DatabaseProviderSettings.GetDisplayName)
            .ToList();

        var currentProviderIndex = Array.IndexOf(DatabaseProviderSettings.AvailableProviders, _databaseSettings.CurrentProvider);
        _suppressToggle = true;
        DatabasePicker.SelectedIndex = currentProviderIndex >= 0 ? currentProviderIndex : 0;
        _suppressToggle = false;
    }

    private void ApplyLocalizedChrome()
    {
        Title = LocalizationUtils.GetStringByKey("settings.title");
        TitleLabel.Text = Title;
        LanguageSectionLabel.Text = LocalizationUtils.GetStringByKey("lang.section");
        AppearanceLabel.Text = LocalizationUtils.GetStringByKey("settings.appearance");
        BluetoothLabel.Text = LocalizationUtils.GetStringByKey("settings.bluetooth");
        UdpLabel.Text = LocalizationUtils.GetStringByKey("settings.udp");
        LanLabel.Text = LocalizationUtils.GetStringByKey("settings.lan");
        RoutingLabel.Text = LocalizationUtils.GetStringByKey("settings.routing");
        DatabaseLabel.Text = LocalizationUtils.GetStringByKey("settings.database");
        DatabaseHint.Text = LocalizationUtils.GetStringByKey("settings.database_hint");
        EconomyLabel.Text = LocalizationUtils.GetStringByKey("settings.economy");
        StorageTitleLabel.Text = LocalizationUtils.GetStringByKey("settings.storage");
        ExportKeysButton.Text = LocalizationUtils.GetStringByKey("settings.export_keys");
        ExportProfileButton.Text = LocalizationUtils.GetStringByKey("settings.export_profile");
        ShareProfileButton.Text = LocalizationUtils.GetStringByKey("settings.share_profile");
        RoutingOpenButton.Text = LocalizationUtils.GetStringByKey("settings.routing_open");
        ConnectionTestButton.Text = LocalizationUtils.GetStringByKey("settings.connection_test");
        LogsButton.Text = LocalizationUtils.GetStringByKey("settings.logs");
        BlacklistButton.Text = LocalizationUtils.GetStringByKey("blacklist.title");
        AboutButton.Text = LocalizationUtils.GetStringByKey("settings.about");
        LogoutButton.Text = LocalizationUtils.GetStringByKey("settings.logout");
        var warn = LanguageService.TranslationWarning(LanguageService.Current);
        LanguageWarningLabel.Text = warn;
        LanguageWarningLabel.IsVisible = LanguageService.ShowTranslationWarning;
    }

    private static readonly AppLanguage[] LanguageOptions =
    [
        AppLanguage.Russian, AppLanguage.English, AppLanguage.Spanish,
        AppLanguage.German, AppLanguage.French, AppLanguage.ChineseSimplified
    ];

    private void RebuildLanguageTiles()
    {
        LanguageTiles.Children.Clear();
        for (var i = 0; i < LanguageOptions.Length; i++)
        {
            var lang = LanguageOptions[i];
            LanguageTiles.Add(CreateLanguageTile(lang, lang == LanguageService.Current), i % 3, i / 3);
        }
    }

    private Border CreateLanguageTile(AppLanguage lang, bool selected)
    {
        var surface = TorgLinkTheme.Current.Surface;
        var tile = new Border
        {
            StrokeThickness = selected ? 2.5 : 1,
            Stroke = selected ? TorgLinkTheme.Accent : TorgLinkTheme.Current.Hairline,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = selected ? Mix(surface, TorgLinkTheme.Accent, 0.18f) : surface,
            Padding = new Thickness(8, 14),
            HeightRequest = 112
        };
        tile.Content = new VerticalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Border
                {
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    WidthRequest = 42,
                    HeightRequest = 28,
                    HorizontalOptions = LayoutOptions.Center,
                    Padding = 0,
                    Content = new Image
                    {
                        Source = LanguageService.FlagImage(lang),
                        Aspect = Aspect.AspectFill,
                        WidthRequest = 42,
                        HeightRequest = 28
                    }
                },
                new Label
                {
                    Text = LanguageService.NativeName(lang),
                    FontSize = 13,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = TorgLinkTheme.Text,
                    LineBreakMode = LineBreakMode.TailTruncation
                }
            }
        };
        var captured = lang;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (captured == LanguageService.Current)
                return;
            var warn = LanguageService.TranslationWarning(captured);
            if (!string.IsNullOrEmpty(warn))
                _ = DisplayAlert(LanguageService.NativeName(captured), warn, LocalizationUtils.GetStringByKey("ok"));
            LanguageService.Set(captured);
        };
        tile.GestureRecognizers.Add(tap);
        return tile;
    }

    private static Color Mix(Color a, Color b, float t) =>
        Color.FromRgba(
            a.Red + (b.Red - a.Red) * t,
            a.Green + (b.Green - a.Green) * t,
            a.Blue + (b.Blue - a.Blue) * t,
            a.Alpha + (b.Alpha - a.Alpha) * t);

    private void RebuildThemeChips()
    {
        ThemeChips.Children.Clear();
        foreach (var kind in ThemeCatalog.All)
        {
            var palette = ThemeCatalog.Get(kind);
            var selected = kind == ThemeService.CurrentKind;
            var swatch = new Border
            {
                WidthRequest = 36,
                HeightRequest = 36,
                StrokeThickness = selected ? 3 : 1,
                Stroke = selected ? palette.TextPrimary : palette.Hairline,
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                BackgroundColor = palette.Accent,
                HorizontalOptions = LayoutOptions.Center
            };
            var chip = new VerticalStackLayout
            {
                Spacing = 6,
                WidthRequest = 72
            };
            chip.Children.Add(swatch);
            chip.Children.Add(new Label
            {
                Text = palette.Title,
                FontSize = 11,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = selected ? TorgLinkTheme.Accent : TorgLinkTheme.Muted
            });
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                ThemeService.Apply(kind);
                RebuildThemeChips();
            };
            chip.GestureRecognizers.Add(tap);
            ThemeChips.Children.Add(chip);
        }
    }

    private void RebuildEconomyChips()
    {
        EconomyChips.Children.Clear();
        var current = _p2p.Settings.TrafficQuality;
        foreach (var mode in MediaEconomy.AllModes)
        {
            var selected = mode == current;
            var btn = new Button
            {
                Text = MediaEconomy.ModeLabel(mode),
                FontSize = 13,
                BackgroundColor = selected ? TorgLinkTheme.Accent : TorgLinkTheme.Current.Surface,
                TextColor = selected ? TorgLinkTheme.Current.ButtonText : TorgLinkTheme.Text,
                Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 0, 8, 8)
            };
            var captured = mode;
            btn.Clicked += (_, _) => _ = SelectEconomyModeAsync(captured);
            EconomyChips.Children.Add(btn);
        }
    }

    private async Task SelectEconomyModeAsync(TrafficQualityMode mode)
    {
        if (_suppressToggle || mode == _p2p.Settings.TrafficQuality)
            return;

        EconomyHint.Text = MediaEconomy.Hint(mode);
        MediaEconomy.Apply(_p2p, mode);
        var ok = await SaveAsync(s => s.TrafficQuality = mode).ConfigureAwait(true);
        if (!ok)
        {
            // Revert UI to whatever is actually persisted.
            var persisted = await _store.LoadAsync().ConfigureAwait(true);
            MediaEconomy.Apply(_p2p, persisted.TrafficQuality);
            EconomyHint.Text = MediaEconomy.Hint(persisted.TrafficQuality);
        }

        RebuildEconomyChips();
    }

    private static string FormatStorage()
    {
        try
        {
            long bytes = 0;
            var dir = FileSystem.AppDataDirectory;
            if (Directory.Exists(dir))
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    bytes += new FileInfo(f).Length;
            var mb = bytes / (1024.0 * 1024.0);
            return mb >= 1024 ? LocalizationUtils.GetStringByKeyWithFormat("settings.storage_gb", mb / 1024) : LocalizationUtils.GetStringByKeyWithFormat("settings.storage_mb", mb);
        }
        catch
        {
            return "—";
        }
    }

    private void OnDatabaseProviderChanged(object? sender, EventArgs e)
    {
        if (_suppressToggle || DatabasePicker.SelectedIndex < 0)
            return;

        var selectedProvider = DatabaseProviderSettings.AvailableProviders[DatabasePicker.SelectedIndex];
        if (selectedProvider == _databaseSettings.CurrentProvider)
            return;

        // Show alert that app needs to restart
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var result = await DisplayAlert(
                LocalizationUtils.GetStringByKey("settings.database_change_title"),
                LocalizationUtils.GetStringByKey("settings.database_change_message"),
                LocalizationUtils.GetStringByKey("ok"),
                LocalizationUtils.GetStringByKey("cancel")
            ).ConfigureAwait(true);

            if (result)
            {
                _databaseSettings.CurrentProvider = selectedProvider;
                AppLog.Settings.Log(LogLevel.Debug, $"Database provider changed to: {selectedProvider}. Application restart is required.");
                // Request app restart
                await Shell.Current.GoToAsync("..").ConfigureAwait(true);
                // Optionally, you could call Application.Current?.Quit() to force restart
            }
            else
            {
                // Revert picker to previous value
                InitializeDatabaseProviderPicker();
            }
        });
    }

    private async void OnBluetoothToggled(object? sender, ToggledEventArgs e)
    {
        BluetoothHint.Text = e.Value ? LocalizationUtils.GetStringByKey("on") : LocalizationUtils.GetStringByKey("off");
        if (!_suppressToggle)
            await SaveAsync(s => s.EnableBluetoothTransport = e.Value).ConfigureAwait(true);
    }

    private async void OnLanToggled(object? sender, ToggledEventArgs e)
    {
        LanHint.Text = e.Value ? LocalizationUtils.GetStringByKey("on") : LocalizationUtils.GetStringByKey("off");
        if (!_suppressToggle)
            await SaveAsync(s => s.EnableUdpTransport = e.Value).ConfigureAwait(true);
    }

    private async void OnRoutingToggled(object? sender, ToggledEventArgs e)
    {
        RoutingHint.Text = e.Value ? LocalizationUtils.GetStringByKey("on") : LocalizationUtils.GetStringByKey("off");
        if (_suppressToggle)
            return;
        await SaveAsync(s =>
        {
            var cap = (s.AdvertisedPeerCapabilities & ~PresencePeerCapabilities.PeerSearch) |
                      PresencePeerCapabilities.Chat;
            if (e.Value)
                cap |= PresencePeerCapabilities.PeerSearch;
            s.AdvertisedPeerCapabilities = cap;
        }).ConfigureAwait(true);
    }

    private async Task<bool> SaveAsync(Action<P2pRoutingSettings> mutate)
    {
        try
        {
            var s = await _store.LoadAsync().ConfigureAwait(true);
            mutate(s);
            await _store.SaveAsync(s).ConfigureAwait(true);
            AppLog.SettingChanged("EnableBluetoothTransport", s.EnableBluetoothTransport);
            AppLog.SettingChanged("EnableUdpTransport", s.EnableUdpTransport);
            AppLog.SettingChanged("AdvertisedPeerCapabilities", s.AdvertisedPeerCapabilities);
            AppLog.SettingChanged("TrafficQuality", s.TrafficQuality);
            _p2p.Settings.EnableUdpTransport = s.EnableUdpTransport;
            _p2p.Settings.EnableBluetoothTransport = s.EnableBluetoothTransport;
            _p2p.Settings.AdvertisedPeerCapabilities = s.AdvertisedPeerCapabilities | PresencePeerCapabilities.Chat;
            MediaEconomy.Apply(_p2p, s.TrafficQuality);
            _bluetoothTransport.ApplySettings(s);
            Header.Bind(_auth.CurrentUser, _p2p);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Save settings toggle");
            return false;
        }
    }

    private async void OnExportKeysClicked(object? sender, EventArgs e) =>
        await ProfileShare.CopyKeysAsync(this, _auth).ConfigureAwait(true);

    private async void OnExportProfileClicked(object? sender, EventArgs e) =>
        await ProfileFileShare.ExportProfileAsync(this, _auth, _profileBackup, _logger).ConfigureAwait(true);

    private async void OnShareProfileClicked(object? sender, EventArgs e) =>
        await ProfileFileShare.ShareProfileAsync(this, _auth, _profileBackup, _logger).ConfigureAwait(true);

    private async void OnOpenRoutingClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<RoutingSettingsPage>()).ConfigureAwait(true);

    private async void OnConnectionTestClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<LanScanPage>()).ConfigureAwait(true);

    private async void OnBlacklistClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<BlacklistPage>()).ConfigureAwait(true);

    private async void OnLogsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<LogsPage>()).ConfigureAwait(true);

    private async void OnAboutClicked(object? sender, EventArgs e) =>
        await DisplayAlert("TorgLink", LocalizationUtils.GetStringByKey("settings.about_body"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (!LogoutButton.IsEnabled)
            return;
        LogoutButton.IsEnabled = false;

        try
        {
            await _auth.LogoutAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logout failed");
        }

        AppLog.Ui.LogInformation("Logout");
        ShowLogin();

        var p2p = _p2p;
        var lora = _lora;
        var logger = _logger;
        // Stop after the login page is up. Awaiting this on the click freezes the UI
        // for the messenger long-poll (and can deadlock WinRT stops on the UI thread).
        P2pBootstrap.TrackShutdown(Task.Run(async () =>
        {
            try
            {
                await p2p.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stop P2P on logout");
            }

            try
            {
                await lora.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stop LoRa on logout");
            }
        }));
    }

    private static void ShowLogin()
    {
        var login = new NavigationPage(MauiProgram.Services.GetRequiredService<LoginPage>());
        var app = Application.Current;
        if (app == null)
            return;
        // MainPage alone does not replace a Shell window that is already open.
        if (app.Windows.Count > 0)
            app.Windows[0].Page = login;
        app.MainPage = login;
    }
}

