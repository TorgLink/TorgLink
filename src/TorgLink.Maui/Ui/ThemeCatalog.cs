using TorgLink.Localization;
namespace TorgLink.Maui;

public enum ThemeKind
{
    DarkFlame,
    Night,
    LightFlame,
    ColdBlue,
    Forest,
    Mono
}

public sealed record ThemePalette(
    string TitleKey,
    bool IsDark,
    Color Accent,
    Color AccentDark,
    Color PageBackground,
    Color Surface,
    Color FieldBackground,
    Color TextPrimary,
    Color Muted,
    Color Hairline,
    Color IncomingBubble,
    Color OutgoingBubble,
    Color SentText,
    Color Check,
    Color ButtonText,
    Color Online,
    Color Offline,
    Color Danger)
{
    public string Title => LocalizationUtils.GetStringByKey(TitleKey);
}

public static class ThemeCatalog
{
    public static readonly ThemeKind[] All =
    [
        ThemeKind.DarkFlame,
        ThemeKind.Night,
        ThemeKind.LightFlame,
        ThemeKind.ColdBlue,
        ThemeKind.Forest,
        ThemeKind.Mono
    ];

    public static ThemePalette Get(ThemeKind kind) => kind switch
    {
        ThemeKind.Night => Night,
        ThemeKind.LightFlame => LightFlame,
        ThemeKind.ColdBlue => ColdBlue,
        ThemeKind.Forest => Forest,
        ThemeKind.Mono => Mono,
        _ => DarkFlame
    };

    private static readonly ThemePalette DarkFlame = new(
        "theme.dark",
        true,
        Color.FromArgb("#FF6A00"),
        Color.FromArgb("#E55E00"),
        Color.FromArgb("#121212"),
        Color.FromArgb("#1E1E20"),
        Color.FromArgb("#2A2A2C"),
        Color.FromArgb("#FFFFFF"),
        Color.FromArgb("#8E8E93"),
        Color.FromArgb("#2C2C2E"),
        Color.FromArgb("#2A2A2C"),
        Color.FromArgb("#FF6A00"),
        Color.FromArgb("#FFFFFF"),
        Color.FromArgb("#5AC8FA"),
        Color.FromArgb("#FFFFFF"),
        Color.FromArgb("#34C759"),
        Color.FromArgb("#636366"),
        Color.FromArgb("#FF453A"));

    private static readonly ThemePalette Night = new(
        "theme.night",
        true,
        Color.FromArgb("#6B8AFF"),
        Color.FromArgb("#5470E0"),
        Color.FromArgb("#000000"),
        Color.FromArgb("#0E0E12"),
        Color.FromArgb("#1A1A22"),
        Color.FromArgb("#E8EAF0"),
        Color.FromArgb("#6E7380"),
        Color.FromArgb("#1C1C24"),
        Color.FromArgb("#16161C"),
        Color.FromArgb("#6B8AFF"),
        Color.FromArgb("#0A0A0E"),
        Color.FromArgb("#9EB0FF"),
        Color.FromArgb("#0A0A0E"),
        Color.FromArgb("#34C759"),
        Color.FromArgb("#4A4A52"),
        Color.FromArgb("#FF453A"));

    private static readonly ThemePalette LightFlame = new(
        "theme.light",
        false,
        Color.FromArgb("#E39B2B"),
        Color.FromArgb("#C4841F"),
        Color.FromArgb("#FFFFFF"),
        Color.FromArgb("#F7F7F8"),
        Color.FromArgb("#F4F4F6"),
        Color.FromArgb("#1C1C1E"),
        Color.FromArgb("#8E8E93"),
        Color.FromArgb("#E8E8ED"),
        Color.FromArgb("#F2F2F2"),
        Color.FromArgb("#FFFFFF"),
        Color.FromArgb("#2B7DE9"),
        Color.FromArgb("#2B7DE9"),
        Color.FromArgb("#FFFFFF"),
        Color.FromArgb("#34C759"),
        Color.FromArgb("#C7C7CC"),
        Color.FromArgb("#D94C4C"));

    private static readonly ThemePalette ColdBlue = new(
        "theme.cold",
        true,
        Color.FromArgb("#00B0FF"),
        Color.FromArgb("#0091EA"),
        Color.FromArgb("#0B1220"),
        Color.FromArgb("#151C2C"),
        Color.FromArgb("#1C2740"),
        Color.FromArgb("#F2F7FF"),
        Color.FromArgb("#8AA0B8"),
        Color.FromArgb("#243044"),
        Color.FromArgb("#1C2740"),
        Color.FromArgb("#00B0FF"),
        Color.FromArgb("#0B1220"),
        Color.FromArgb("#7FDBFF"),
        Color.FromArgb("#0B1220"),
        Color.FromArgb("#34C759"),
        Color.FromArgb("#5B6B7A"),
        Color.FromArgb("#FF6B6B"));

    private static readonly ThemePalette Forest = new(
        "theme.forest",
        true,
        Color.FromArgb("#00C853"),
        Color.FromArgb("#00A844"),
        Color.FromArgb("#0E1510"),
        Color.FromArgb("#18231B"),
        Color.FromArgb("#223328"),
        Color.FromArgb("#F3FFF6"),
        Color.FromArgb("#8AA894"),
        Color.FromArgb("#2A3A30"),
        Color.FromArgb("#223328"),
        Color.FromArgb("#00C853"),
        Color.FromArgb("#0E1510"),
        Color.FromArgb("#69F0AE"),
        Color.FromArgb("#0E1510"),
        Color.FromArgb("#69F0AE"),
        Color.FromArgb("#5B6B60"),
        Color.FromArgb("#FF5252"));

    private static readonly ThemePalette Mono = new(
        "theme.mono",
        true,
        Color.FromArgb("#E6E6E6"),
        Color.FromArgb("#C8C8C8"),
        Color.FromArgb("#111111"),
        Color.FromArgb("#1A1A1A"),
        Color.FromArgb("#2A2A2A"),
        Color.FromArgb("#F5F5F5"),
        Color.FromArgb("#9A9A9A"),
        Color.FromArgb("#2E2E2E"),
        Color.FromArgb("#2A2A2A"),
        Color.FromArgb("#3A3A3A"),
        Color.FromArgb("#F5F5F5"),
        Color.FromArgb("#F5F5F5"),
        Color.FromArgb("#111111"),
        Color.FromArgb("#34C759"),
        Color.FromArgb("#666666"),
        Color.FromArgb("#FF5252"));
}
