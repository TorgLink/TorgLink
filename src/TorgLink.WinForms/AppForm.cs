using TorgLink.Localization;

namespace TorgLink.WinForms;

/// <summary>Base for all TorgLink WinForms windows: default UI font 12 pt + live language refresh.</summary>
public class AppForm : Form
{
    protected AppForm()
    {
        Font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);
        Branding.ApplyFormIcon(this);
        LanguageService.Changed += OnLanguageChanged;
        HandleDestroyed += (_, _) => LanguageService.Changed -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
            return;
        // Skip while the derived constructor may still be wiring controls (e.g. TabPages).
        // Forms apply localization explicitly at the end of their ctor and/or on Load.
        if (!IsHandleCreated)
            return;

        try
        {
            BeginInvoke(new Action(ApplyLocalizedUi));
        }
        catch (ObjectDisposedException)
        {
            // form closing
        }
        catch (InvalidOperationException)
        {
            ApplyLocalizedUi();
        }
    }

    /// <summary>Re-apply UI strings from <see cref="LocalizationUtils"/>. Designer literals remain as fallback.</summary>
    protected virtual void ApplyLocalizedUi()
    {
    }
}
