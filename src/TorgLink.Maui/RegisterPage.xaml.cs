using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using TorgLink.Localization;

namespace TorgLink.Maui;

public partial class RegisterPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ILogger<RegisterPage> _logger;

    public RegisterPage(AuthService auth, ILogger<RegisterPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _logger = logger;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedUi();
        SetPasswordHint(RequirementsHint(), muted: true);
    }

    private static string RequirementsHint() =>
        LocalizationUtils.GetStringByKeyWithFormat("register.pass_hint", UserPasswordPolicy.AllowedSpecialCharacters);

    private void ApplyLocalizedUi()
    {
        Title = LocalizationUtils.GetStringByKey("register.header");
        TitleLabel.Text = LocalizationUtils.GetStringByKey("register.title");
        SubtitleLabel.Text = LocalizationUtils.GetStringByKey("register.subtitle");
        NicknameEntry.Placeholder = LocalizationUtils.GetStringByKey("login.nick");
        PasswordEntry.Placeholder = LocalizationUtils.GetStringByKey("login.password");
        RegisterButton.Text = LocalizationUtils.GetStringByKey("register.button");
    }

    private void OnPasswordTextChanged(object? sender, TextChangedEventArgs e)
    {
        var pass = e.NewTextValue ?? "";
        if (string.IsNullOrEmpty(pass))
        {
            SetPasswordHint(RequirementsHint(), muted: true);
            return;
        }

        if (UserPasswordPolicy.TryValidate(pass, out var error))
        {
            SetPasswordHint(LocalizationUtils.GetStringByKey("register.pass_ok"), muted: true, ok: true);
            return;
        }

        SetPasswordHint(DescribePasswordError(error!.Value), muted: false);
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        var nick = NicknameEntry.Text?.Trim() ?? "";
        var pass = PasswordEntry.Text ?? "";

        if (!UserPasswordPolicy.TryValidate(pass, out var policyError))
        {
            var reason = DescribePasswordError(policyError!.Value);
            _logger.LogWarning("Registration failed for {Nickname}: {Reason}", nick, reason);
            await DisplayAlert(LocalizationUtils.GetStringByKey("register.header"), reason, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        var (ok, err) = await _auth.RegisterAsync(nick, pass).ConfigureAwait(true);
        if (!ok)
        {
            _logger.LogWarning("Registration failed for {Nickname}: {Reason}", nick, err);
            await DisplayAlert(LocalizationUtils.GetStringByKey("register.header"), LocalizeRegisterError(err), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return;
        }

        var id = _auth.CurrentUser?.NetworkIdShort ?? "";
        AppLog.Ui.LogInformation("Registration success for {Nickname} id={Id}", nick, id);
        await DisplayAlert(LocalizationUtils.GetStringByKey("register.created"), LocalizationUtils.GetStringByKeyWithFormat("register.network_id", id), LocalizationUtils.GetStringByKey("ok"))
            .ConfigureAwait(true);

        Application.Current!.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
    }

    private void SetPasswordHint(string text, bool muted, bool ok = false)
    {
        PasswordHintLabel.Text = text;
        var resources = Application.Current?.Resources;
        if (ok && resources?["OnlineGreen"] is Color okColor)
            PasswordHintLabel.TextColor = okColor;
        else if (!muted && resources?["Danger"] is Color danger)
            PasswordHintLabel.TextColor = danger;
        else if (resources?["MutedText"] is Color mutedColor)
            PasswordHintLabel.TextColor = mutedColor;
    }

    private static string DescribePasswordError(UserPasswordPolicyError error) => error switch
    {
        UserPasswordPolicyError.Empty => LocalizationUtils.GetStringByKey("pass.empty"),
        UserPasswordPolicyError.TooShort => LocalizationUtils.GetStringByKey("pass.too_short"),
        UserPasswordPolicyError.InvalidCharacter =>
            LocalizationUtils.GetStringByKeyWithFormat("pass.invalid_char", UserPasswordPolicy.AllowedSpecialCharacters),
        UserPasswordPolicyError.MissingUppercase => LocalizationUtils.GetStringByKey("pass.need_upper"),
        UserPasswordPolicyError.MissingLetter => LocalizationUtils.GetStringByKey("pass.need_letter"),
        UserPasswordPolicyError.MissingDigit => LocalizationUtils.GetStringByKey("pass.need_digit"),
        _ => LocalizationUtils.GetStringByKey("pass.invalid")
    };

    private static string LocalizeRegisterError(string? err) => err switch
    {
        "Nickname and password are required." => LocalizationUtils.GetStringByKey("register.need_nick_pass"),
        "This nickname is already registered." => LocalizationUtils.GetStringByKey("register.nick_taken"),
        _ => err ?? LocalizationUtils.GetStringByKey("register.failed")
    };
}

