using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.ProfileBackup;
using ShortP2P.Client.Services;
using TorgLink.Localization;

namespace TorgLink.Maui;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ILogger<LoginPage> _logger;
    private readonly ProfileBackupService _profileBackup;

    public LoginPage(AuthService auth, ILogger<LoginPage> logger, ProfileBackupService profileBackup)
    {
        InitializeComponent();
        _auth = auth;
        _logger = logger;
        _profileBackup = profileBackup;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedUi();
        if (await _auth.TryRestoreSessionAsync().ConfigureAwait(true) && _auth.CurrentUser != null)
            await GoToChatsAsync().ConfigureAwait(true);
    }

    private void ApplyLocalizedUi()
    {
        SubtitleLabel.Text = LocalizationUtils.GetStringByKey("login.subtitle");
        NicknameEntry.Placeholder = LocalizationUtils.GetStringByKey("login.nick");
        PasswordEntry.Placeholder = LocalizationUtils.GetStringByKey("login.password");
        SignInButton.Text = LocalizationUtils.GetStringByKey("login.sign_in");
        CreateAccountButton.Text = LocalizationUtils.GetStringByKey("login.create_account");
        ImportProfileButton.Text = LocalizationUtils.GetStringByKey("login.import_profile");
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var nick = NicknameEntry.Text?.Trim() ?? "";
        var pass = PasswordEntry.Text ?? "";
        var (ok, err) = await _auth.LoginAsync(nick, pass).ConfigureAwait(true);
        if (!ok)
        {
            _logger.LogWarning("Login failed for {Nickname}: {Reason}", nick, err);
            await DisplayAlert(LocalizationUtils.GetStringByKey("login.sign_in"), err ?? LocalizationUtils.GetStringByKey("login.failed"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return;
        }

        AppLog.Ui.LogInformation("Login success for {Nickname}", nick);
        await GoToChatsAsync().ConfigureAwait(true);
    }

    private async void OnRegisterNavigateClicked(object? sender, EventArgs e)
    {
        var page = MauiProgram.Services.GetRequiredService<RegisterPage>();
        await Navigation.PushAsync(page).ConfigureAwait(true);
    }

    private async void OnImportProfileClicked(object? sender, EventArgs e)
    {
        var imported = await ProfileFileShare.ImportProfileAsync(this, _auth, _profileBackup, _logger)
            .ConfigureAwait(true);
        if (!imported)
            return;

        AppLog.Ui.LogInformation("Profile import success for {Nickname}", _auth.CurrentUser?.Nickname ?? "");
        await GoToChatsAsync().ConfigureAwait(true);
    }

    private async Task GoToChatsAsync()
    {
        try
        {
            var chats = MauiProgram.Services.GetRequiredService<ChatRepository>();
            var migration = await IdentityMigrationApplier.ApplyPendingAsync(_auth, chats, _logger)
                .ConfigureAwait(true);
            if (migration != null)
                await DisplayAlert(
                    LocalizationUtils.GetStringByKey("ok"),
                    migration,
                    LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Identity migration after login");
        }

        Application.Current!.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
    }
}

