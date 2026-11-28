using System.Globalization;
using TorgLink.Localization;
using TorgLink.Maui.Services;

namespace TorgLink.Maui.Localization;

internal static class MauiLanguageBootstrap
{
    private static bool _hooked;

    public static void Initialize()
    {
        // Shared portable LanguageService (not the removed MAUI-local duplicate).
        TorgLink.Localization.LanguageService.Configure(new MauiLanguagePreferences(), ApplyCulture);
        if (!_hooked)
        {
            _hooked = true;
            TorgLink.Localization.LanguageService.Changed += (_, _) =>
                AppLog.SettingChanged("Language", TorgLink.Localization.LanguageService.Current);
        }

        TorgLink.Localization.LanguageService.Load();
    }

    private static void ApplyCulture(TorgLink.Localization.AppLanguage language)
    {
        var culture = TorgLink.Localization.LanguageService.ToCulture(language);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
