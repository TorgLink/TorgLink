namespace TorgLink.WinForms;

/// <summary>
/// TRL-10: modal password prompt — one secure field, or two for "new password + confirm".
/// Returns the entered password, or null when cancelled.
/// </summary>
public sealed class PasswordPromptForm : AppForm
{
    private readonly TextBox _password;
    private readonly TextBox? _confirm;
    private readonly Label _error;

    private PasswordPromptForm(string title, string message, string caption, string? confirmCaption)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var messageLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 0, 0, 8),
            Text = message
        };
        layout.Controls.Add(messageLabel);

        var captionLabel = new Label { AutoSize = true, Text = caption };
        layout.Controls.Add(captionLabel);

        _password = new TextBox { Width = 320, UseSystemPasswordChar = true };
        layout.Controls.Add(_password);

        if (confirmCaption != null)
        {
            var confirmLabel = new Label { AutoSize = true, Text = confirmCaption };
            layout.Controls.Add(confirmLabel);
            _confirm = new TextBox { Width = 320, UseSystemPasswordChar = true };
            layout.Controls.Add(_confirm);
        }

        _error = new Label
        {
            AutoSize = true,
            ForeColor = Color.Firebrick,
            MaximumSize = new Size(420, 0),
            Visible = false
        };
        layout.Controls.Add(_error);

        var ok = new Button { Text = "OK", AutoSize = true };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => OnOk();

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0)
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    /// <summary>Single password entry. Null when the user cancels.</summary>
    public static string? Prompt(IWin32Window owner, string title, string message, string caption)
    {
        using var form = new PasswordPromptForm(title, message, caption, confirmCaption: null);
        return form.ShowDialog(owner) == DialogResult.OK ? form._password.Text : null;
    }

    /// <summary>Two entries that must match (new master password). Null when the user cancels.</summary>
    public static string? PromptNew(
        IWin32Window owner,
        string title,
        string message,
        string caption,
        string confirmCaption)
    {
        using var form = new PasswordPromptForm(title, message, caption, confirmCaption);
        return form.ShowDialog(owner) == DialogResult.OK ? form._password.Text : null;
    }

    private void OnOk()
    {
        if (string.IsNullOrEmpty(_password.Text))
        {
            ShowError("Введите пароль.");
            return;
        }

        if (_confirm != null && !string.Equals(_password.Text, _confirm.Text, StringComparison.Ordinal))
        {
            ShowError("Пароли не совпадают.");
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowError(string text)
    {
        _error.Text = text;
        _error.Visible = true;
    }
}
