using TorgLink.Localization;

namespace TorgLink.WinForms.Localization;

/// <summary>3×2 flag tiles for language selection / settings.</summary>
internal sealed class LanguageTilesPanel : Panel
{
    private const int TileWidth = 120;
    private const int TileHeight = 88;
    private const int Gap = 12;
    private const int Columns = 3;

    private AppLanguage _selected;
    private readonly Action<AppLanguage>? _onSelected;
    private readonly Dictionary<AppLanguage, Panel> _tiles = new();

    public LanguageTilesPanel(AppLanguage selected, Action<AppLanguage>? onSelected = null)
    {
        _selected = selected;
        _onSelected = onSelected;

        var langs = LanguageService.AllLanguages;
        var rows = Math.Max(1, (langs.Length + Columns - 1) / Columns);
        var grid = new Size(
            Columns * TileWidth + (Columns - 1) * Gap,
            rows * TileHeight + (rows - 1) * Gap);
        Size = grid;
        MinimumSize = grid;
        MaximumSize = grid;
        Anchor = AnchorStyles.None;

        for (var i = 0; i < langs.Length; i++)
        {
            var lang = langs[i];
            var tile = CreateTile(lang);
            tile.Location = new Point(
                (i % Columns) * (TileWidth + Gap),
                (i / Columns) * (TileHeight + Gap));
            _tiles[lang] = tile;
            Controls.Add(tile);
        }

        HighlightSelected();
    }

    public void SetSelected(AppLanguage language)
    {
        _selected = language;
        HighlightSelected();
    }

    private Panel CreateTile(AppLanguage language)
    {
        var panel = new Panel
        {
            Width = TileWidth,
            Height = TileHeight,
            Margin = Padding.Empty,
            Cursor = Cursors.Hand,
            Tag = language
        };

        var flag = new PictureBox
        {
            Width = 54,
            Height = 36,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(33, 10),
            Image = FlagImages.Load(language)
        };
        var label = new Label
        {
            Text = LanguageService.NativeName(language),
            AutoSize = false,
            Width = 110,
            Height = 28,
            Location = new Point(5, 52),
            TextAlign = ContentAlignment.TopCenter
        };

        void ClickHandler(object? s, EventArgs e)
        {
            _selected = language;
            HighlightSelected();
            _onSelected?.Invoke(language);
        }

        panel.Click += ClickHandler;
        flag.Click += ClickHandler;
        label.Click += ClickHandler;
        panel.Controls.Add(flag);
        panel.Controls.Add(label);
        return panel;
    }

    private void HighlightSelected()
    {
        foreach (var kv in _tiles)
        {
            var selected = kv.Key == _selected;
            kv.Value.BackColor = selected ? Color.FromArgb(220, 235, 255) : SystemColors.Control;
            kv.Value.Padding = new Padding(selected ? 2 : 0);
            kv.Value.BorderStyle = selected ? BorderStyle.FixedSingle : BorderStyle.None;
        }
    }
}
