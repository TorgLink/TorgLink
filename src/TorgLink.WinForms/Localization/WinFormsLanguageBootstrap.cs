using System.Globalization;
using TorgLink.Localization;

namespace TorgLink.WinForms.Localization;

internal static class WinFormsLanguageBootstrap
{
    public static void Initialize(string appRoot)
    {
        LanguageService.Configure(new FileLanguagePreferences(appRoot), ApplyCulture);
        LanguageService.Load();
    }

    private static void ApplyCulture(AppLanguage language)
    {
        var culture = LanguageService.ToCulture(language);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch
        {
            // Some hosts disallow setting CurrentCulture; DefaultThread* is enough for new threads.
        }

        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
    }
}
