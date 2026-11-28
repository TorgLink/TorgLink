using TorgLink.Localization;

namespace TorgLink.Maui.Localization;

internal sealed class MauiLanguagePreferences : ILanguagePreferences
{
    private const string PrefLanguage = "torglink_language";
    private const string PrefChosen = "torglink_language_chosen";

    public string GetLanguage() =>
        Preferences.Default.Get(PrefLanguage, nameof(AppLanguage.Russian));

    public void SetLanguage(string language) =>
        Preferences.Default.Set(PrefLanguage, language);

    public bool GetChosen() => Preferences.Default.Get(PrefChosen, false);

    public void SetChosen(bool chosen) => Preferences.Default.Set(PrefChosen, chosen);
}
