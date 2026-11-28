namespace TorgLink.Localization
{
    /// <summary>
    /// Platform persistence for the selected UI language.
    /// MAUI uses Preferences; WinForms uses a small file under LocalAppData.
    /// </summary>
    public interface ILanguagePreferences
    {
        string GetLanguage();
        void SetLanguage(string language);
        bool GetChosen();
        void SetChosen(bool chosen);
    }
}
