using TorgLink.Localization;

namespace TorgLink.WinForms.Localization;

/// <summary>Persists language under %LocalAppData%\TorgLink\WinForms\.</summary>
internal sealed class FileLanguagePreferences : ILanguagePreferences
{
    private readonly string _languagePath;
    private readonly string _chosenPath;

    public FileLanguagePreferences(string appRoot)
    {
        Directory.CreateDirectory(appRoot);
        _languagePath = Path.Combine(appRoot, "language.txt");
        _chosenPath = Path.Combine(appRoot, "language_chosen.txt");
    }

    public string GetLanguage()
    {
        try
        {
            if (File.Exists(_languagePath))
            {
                var raw = File.ReadAllText(_languagePath).Trim();
                if (raw.Length > 0)
                    return raw;
            }
        }
        catch
        {
            // best-effort
        }

        return nameof(AppLanguage.Russian);
    }

    public void SetLanguage(string language)
    {
        try
        {
            File.WriteAllText(_languagePath, language ?? nameof(AppLanguage.Russian));
        }
        catch
        {
            // best-effort
        }
    }

    public bool GetChosen()
    {
        try
        {
            if (!File.Exists(_chosenPath))
                return false;
            var raw = File.ReadAllText(_chosenPath).Trim();
            return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void SetChosen(bool chosen)
    {
        try
        {
            File.WriteAllText(_chosenPath, chosen ? "1" : "0");
        }
        catch
        {
            // best-effort
        }
    }
}
