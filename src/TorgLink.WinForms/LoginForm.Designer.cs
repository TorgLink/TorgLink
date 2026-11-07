#nullable enable
namespace TorgLink.WinForms;

partial class LoginForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel _layout;
    private PictureBox? _logo;
    private Label _lblNick;
    private TextBox _nick;
    private Label _lblPass;
    private TextBox _pass;
    private FlowLayoutPanel _buttons;
    private Button _login;
    private Button _register;
    private Button _import;
    private Button _exit;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _layout = new TableLayoutPanel();
        _lblNick = new Label();
        _nick = new TextBox();
        _lblPass = new Label();
        _pass = new TextBox();
        _buttons = new FlowLayoutPanel();
        _login = new Button();
        _register = new Button();
        _import = new Button();
        _exit = new Button();
        _layout.SuspendLayout();
        _buttons.SuspendLayout();
        SuspendLayout();

        _layout.AutoSize = true;
        _layout.ColumnCount = 1;
        _layout.Dock = DockStyle.Fill;
        _layout.Name = "_layout";

        _logo = Branding.CreateLogoPicture(64);
        if (_logo != null)
        {
            _logo.Margin = new Padding(0, 0, 0, 8);
            _logo.Name = "_logo";
            _layout.Controls.Add(_logo);
        }

        _lblNick.AutoSize = true;
        _lblNick.Name = "_lblNick";
        _lblNick.Text = "Ник";
        _layout.Controls.Add(_lblNick);

        _nick.Name = "_nick";
        _nick.Width = 320;
        _layout.Controls.Add(_nick);

        _lblPass.AutoSize = true;
        _lblPass.Name = "_lblPass";
        _lblPass.Text = "Пароль";
        _layout.Controls.Add(_lblPass);

        _pass.Name = "_pass";
        _pass.UseSystemPasswordChar = true;
        _pass.Width = 320;
        _layout.Controls.Add(_pass);

        _login.Name = "_login";
        _login.Text = "Войти";
        _register.Name = "_register";
        _register.Text = "Регистрация";
        _import.Name = "_import";
        _import.Text = "Импорт профиля…";
        _exit.Name = "_exit";
        _exit.Text = "Разлогиниться";

        _buttons.AutoSize = true;
        _buttons.Name = "_buttons";
        _buttons.Controls.Add(_login);
        _buttons.Controls.Add(_register);
        _buttons.Controls.Add(_import);
        _buttons.Controls.Add(_exit);
        _layout.Controls.Add(_buttons);

        AcceptButton = _login;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(_layout);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "LoginForm";
        Padding = new Padding(16);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "TorgLink — вход";

        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        _buttons.ResumeLayout(false);
        _buttons.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
