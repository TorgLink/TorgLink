namespace TorgLink.Localization
{
    /// <summary>UI strings only. Log messages stay in their original language.</summary>
    public static class LocalizationUtils
    {
        public static string GetStringByKey(string key) =>
            StringCatalog.Get(LanguageService.Current, key);

        public static string GetStringByKeyWithFormat(string key, params object[] args) =>
            string.Format(System.Globalization.CultureInfo.CurrentCulture, GetStringByKey(key), args);
    }
}
