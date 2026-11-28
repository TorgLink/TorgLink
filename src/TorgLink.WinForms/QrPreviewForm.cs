using TorgLink.Localization;

namespace TorgLink.WinForms;

public sealed partial class QrPreviewForm : AppForm
{
    private string? _titleKey;

    public QrPreviewForm()
    {
        InitializeComponent();
    }

    public QrPreviewForm(string title, byte[] png, string caption, string? titleKey = null)
        : this()
    {
        _titleKey = titleKey;
        Text = title;
        using var ms = new MemoryStream(png);
        _box.Image = new Bitmap(ms);
        _label.Text = caption;
        ApplyLocalizedUi();
    }

    protected override void ApplyLocalizedUi()
    {
        if (!string.IsNullOrEmpty(_titleKey))
            Text = LocalizationUtils.GetStringByKey(_titleKey);
        _close.Text = LocalizationUtils.GetStringByKey("close");
    }
}
