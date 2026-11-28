using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using TorgLink.Localization;

namespace TorgLink.WinForms;

public sealed partial class RegisterForm : AppForm
{
    private readonly AuthService _auth = null!;
    private readonly ILogger<RegisterForm> _logger = null!;

    public RegisterForm()
    {
        InitializeComponent();
    }

    public RegisterForm(AuthService auth, ILogger<RegisterForm> logger)
        : this()
    {
        _auth = auth;
        _logger = logger;
        _ok.Click += async (_, _) => await OnRegisterAsync().ConfigureAwait(true);
        ApplyLocalizedUi();
    }

    protected override void ApplyLocalizedUi()
    {
        Text = "TorgLink — " + LocalizationUtils.GetStringByKey("register.header");
        _lblNick.Text = LocalizationUtils.GetStringByKey("login.nick");
        _lblPass.Text = LocalizationUtils.GetStringByKey("login.password");
        _ok.Text = LocalizationUtils.GetStringByKey("register.button");
        _cancel.Text = LocalizationUtils.GetStringByKey("cancel");
    }

    private async Task OnRegisterAsync()
    {
        var (ok, err) = await _auth.RegisterAsync(_nick.Text.Trim(), _pass.Text ?? "").ConfigureAwait(true);
        if (!ok)
        {
            _logger.LogWarning("Register failed: {Reason}", err);
            MessageBox.Show(this, err ?? LocalizationUtils.GetStringByKey("register.failed"),
                LocalizationUtils.GetStringByKey("register.header"), MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var id = _auth.CurrentUser?.NetworkIdShort ?? "";
        MessageBox.Show(this,
            LocalizationUtils.GetStringByKeyWithFormat("register.network_id", id),
            LocalizationUtils.GetStringByKey("register.created"), MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
