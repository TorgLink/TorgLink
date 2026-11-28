using TorgLink.Localization;

namespace TorgLink.Maui.Localization;

/// <summary>UI strings only. Log messages stay in their original language.</summary>
internal static class Loc
{
    public static string T(string key) => StringCatalog.Get(LanguageService.Current, key);

    public static string Tf(string key, params object?[] args) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, T(key), args);
}
