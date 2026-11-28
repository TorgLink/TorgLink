using System;
using System.Collections.Generic;

namespace TorgLink.Localization;

public static partial class StringCatalog
{
    // Do NOT cache language tables in a static field initializer that references Ru/En/Es/De/Fr/Zh:
    // those properties are declared later (and across partial files), so the map can capture null
    // and English silently falls back to Russian.
    public static string Get(AppLanguage language, string key)
    {
        var table = TableFor(language);
        if (table.TryGetValue(key, out var value))
            return value;
        if (language != AppLanguage.Russian && Ru.TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }

    private static Dictionary<string, string> TableFor(AppLanguage language) => language switch
    {
        AppLanguage.English => En,
        AppLanguage.Spanish => Es,
        AppLanguage.German => De,
        AppLanguage.French => Fr,
        AppLanguage.ChineseSimplified => Zh,
        _ => Ru
    };
}
