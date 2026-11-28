using TorgLink.Localization;
using TorgLink.WinForms.Localization;

namespace TorgLink.WinForms;

/// <summary>First-launch language picker (shown when language has not been chosen yet).</summary>
public sealed class LanguageSelectForm : AppForm
{
    private AppLanguage _selected = AppLanguage.Russian;
    private readonly Label _title = new();
    private readonly Label _warning = new();
    private readonly Button _continue = new();
    private LanguageTilesPanel? _tiles;

    public LanguageSelectForm()
    {
        SuspendLayout();
        Text = LocalizationUtils.GetStringByKey("lang.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(24);

        var root = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            Dock = DockStyle.Fill
        };

        var logo = Branding.CreateLogoPicture(64);
        if (logo != null)
        {
            logo.Anchor = AnchorStyles.None;
            root.Controls.Add(logo);
        }

        var brand = new Label
        {
            Text = "TorgLink",
            Font = new Font(Font.FontFamily, 18f, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 8, 0, 4)
        };
        root.Controls.Add(brand);

        _title.AutoSize = true;
        _title.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);
        _title.Anchor = AnchorStyles.None;
        _title.Margin = new Padding(0, 8, 0, 12);
        root.Controls.Add(_title);

        _tiles = new LanguageTilesPanel(_selected, lang =>
        {
            _selected = lang;
            // Preview UI in the chosen language; persist only on Continue (markChosen).
            LanguageService.Set(lang, markChosen: false);
        });
        root.Controls.Add(_tiles);

        _warning.AutoSize = true;
        _warning.ForeColor = Color.DarkOrange;
        _warning.MaximumSize = new Size(360, 0);
        _warning.Anchor = AnchorStyles.None;
        _warning.Margin = new Padding(0, 8, 0, 8);
        root.Controls.Add(_warning);

        _continue.AutoSize = true;
        _continue.Anchor = AnchorStyles.None;
        _continue.Margin = new Padding(0, 8, 0, 0);
        _continue.Click += (_, _) =>
        {
            LanguageService.Set(_selected, markChosen: true);
            DialogResult = DialogResult.OK;
            Close();
        };
        root.Controls.Add(_continue);

        Controls.Add(root);
        AcceptButton = _continue;
        ResumeLayout(true);
        ApplyLocalizedUi();
        RefreshWarning();
    }

    protected override void ApplyLocalizedUi()
    {
        Text = LocalizationUtils.GetStringByKey("lang.title");
        _title.Text = LocalizationUtils.GetStringByKey("lang.choose");
        _continue.Text = LocalizationUtils.GetStringByKey("lang.continue");
        RefreshWarning();
    }

    private void RefreshWarning()
    {
        var warn = LanguageService.TranslationWarning(_selected);
        _warning.Text = warn;
        _warning.Visible = !string.IsNullOrEmpty(warn);
    }
}
