using TorgLink.Localization;

namespace TorgLink.WinForms;

/// <summary>
/// TRL-24 (Win7 / WinForms): share a prepared <c>.tlp</c> via email or by copying the file path.
/// </summary>
internal sealed class ProfileShareForm : AppForm
{
    private readonly string _filePath;

    public ProfileShareForm(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);

        Text = LocalizationUtils.GetStringByKey("profilex.share_title");
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

        var warn = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            ForeColor = Color.Firebrick,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10),
            Text = LocalizationUtils.GetStringByKey("profilex.share_channel_warn")
        };
        layout.Controls.Add(warn);

        var fileLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 0, 0, 12),
            Text = LocalizationUtils.GetStringByKeyWithFormat("profilex.share_file", _filePath)
        };
        layout.Controls.Add(fileLabel);

        var email = new Button
        {
            AutoSize = true,
            Text = LocalizationUtils.GetStringByKey("profilex.share_email")
        };
        email.Click += (_, _) => OnEmail();

        var copy = new Button
        {
            AutoSize = true,
            Text = LocalizationUtils.GetStringByKey("profilex.share_copy_path")
        };
        copy.Click += (_, _) => OnCopyPath();

        var close = new Button
        {
            AutoSize = true,
            Text = LocalizationUtils.GetStringByKey("close"),
            DialogResult = DialogResult.OK
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 4, 0, 0)
        };
        buttons.Controls.Add(email);
        buttons.Controls.Add(copy);
        buttons.Controls.Add(close);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
    }

    private void OnEmail()
    {
        if (SimpleMapiMail.TrySendWithAttachment(
                this,
                LocalizationUtils.GetStringByKey("profilex.share_title"),
                LocalizationUtils.GetStringByKey("profilex.share_channel_warn"),
                _filePath,
                out _))
            return;

        MessageBox.Show(this,
            LocalizationUtils.GetStringByKey("profilex.share_email_fail"),
            LocalizationUtils.GetStringByKey("profilex.share_title"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void OnCopyPath()
    {
        try
        {
            Clipboard.SetText("\"" + _filePath + "\"");
            MessageBox.Show(this,
                LocalizationUtils.GetStringByKey("profilex.share_path_copied"),
                LocalizationUtils.GetStringByKey("profilex.share_title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message,
                LocalizationUtils.GetStringByKey("profilex.share_title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
