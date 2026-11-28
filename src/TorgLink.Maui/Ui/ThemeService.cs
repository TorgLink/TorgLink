namespace TorgLink.Maui;

internal static class ThemeService
{
    private const string PrefKey = "torglink_theme";

    public static ThemeKind CurrentKind { get; private set; } = ThemeKind.DarkFlame;

    public static event EventHandler? Changed;

    public static void LoadAndApply()
    {
        var raw = Preferences.Default.Get(PrefKey, nameof(ThemeKind.DarkFlame));
        if (!Enum.TryParse(raw, out ThemeKind kind) || !ThemeCatalog.All.Contains(kind))
            kind = ThemeKind.DarkFlame;
        Apply(kind, save: false, syncOsTheme: false);
    }

    public static void Apply(ThemeKind kind, bool save = true, bool syncOsTheme = true)
    {
        CurrentKind = kind;
        var p = ThemeCatalog.Get(kind);
        TorgLinkTheme.Set(p);

        var app = Application.Current;
        if (app != null)
        {
            Write(app, p);
            if (syncOsTheme)
                app.UserAppTheme = p.IsDark ? AppTheme.Dark : AppTheme.Light;
        }

        if (save)
        {
            Preferences.Default.Set(PrefKey, kind.ToString());
            TorgLink.Maui.Services.AppLog.SettingChanged("Theme", kind);
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void Write(Application app, ThemePalette p)
    {
        Set(app, "Accent", p.Accent);
        Set(app, "AccentDark", p.AccentDark);
        Set(app, "Primary", p.Accent);
        Set(app, "PrimaryDark", p.AccentDark);
        Set(app, "PageBackground", p.PageBackground);
        Set(app, "Surface", p.Surface);
        Set(app, "ChatSidebarBackground", p.IsDark ? Color.FromArgb("#2F4F4F") : p.Surface);
        Set(app, "FieldBackground", p.FieldBackground);
        Set(app, "TextPrimary", p.TextPrimary);
        Set(app, "MidnightBlue", p.TextPrimary);
        Set(app, "MutedText", p.Muted);
        Set(app, "Hairline", p.Hairline);
        Set(app, "IncomingBubble", p.IncomingBubble);
        Set(app, "OutgoingBubble", p.OutgoingBubble);
        Set(app, "SentText", p.SentText);
        Set(app, "Check", p.Check);
        Set(app, "ButtonText", p.ButtonText);
        Set(app, "OnlineGreen", p.Online);
        Set(app, "TabBarBackground", p.Surface);
        Set(app, "Danger", p.Danger);
        Set(app, "Magenta", p.Accent);
        Set(app, "OffBlack", p.PageBackground);
        // Hover/press for chat & contact list rows: gray on dark themes, light-gray on light.
        Set(app, "ListRowHover", p.IsDark
            ? Color.FromArgb("#3A3A3C")
            : Color.FromArgb("#E5E5EA"));

        app.Resources["PrimaryBrush"] = new SolidColorBrush(p.Accent);
        app.Resources["SecondaryBrush"] = new SolidColorBrush(p.Surface);
        app.Resources["TertiaryBrush"] = new SolidColorBrush(p.AccentDark);
    }

    private static void Set(Application app, string key, Color color)
    {
        app.Resources[key] = color;
    }
}
