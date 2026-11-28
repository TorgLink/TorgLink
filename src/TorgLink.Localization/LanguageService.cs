using System;
using System.Globalization;

namespace TorgLink.Localization
{
    /// <summary>
    /// Portable language state: Current, culture mapping, persistence via <see cref="ILanguagePreferences"/>.
    /// Platforms register preferences and optionally a culture applicator at startup
    /// (netstandard1.1 cannot set DefaultThreadCurrentCulture).
    /// </summary>
    public static class LanguageService
    {
        private static ILanguagePreferences _preferences;
        private static Action<AppLanguage> _applyCulture;

        public static AppLanguage Current { get; private set; } = AppLanguage.Russian;

        public static bool HasChosen =>
            _preferences != null && _preferences.GetChosen();

        /// <summary>Only Simplified Chinese shows the translation-inaccuracy disclaimer.</summary>
        public static bool ShowTranslationWarning => Current == AppLanguage.ChineseSimplified;

        public static event EventHandler Changed;

        /// <summary>
        /// Must be called once at app startup before <see cref="Load"/>.
        /// <paramref name="applyCulture"/> applies CultureInfo on the host (MAUI / WinForms).
        /// </summary>
        public static void Configure(ILanguagePreferences preferences, Action<AppLanguage> applyCulture)
        {
            if (preferences == null)
                throw new ArgumentNullException(nameof(preferences));
            _preferences = preferences;
            _applyCulture = applyCulture;
        }

        public static void Load()
        {
            EnsureConfigured();
            var raw = _preferences.GetLanguage();
            if (string.IsNullOrEmpty(raw) || !TryParseLanguage(raw, out var lang))
                lang = AppLanguage.Russian;
            Apply(lang, save: false, markChosen: false);
        }

        public static void Set(AppLanguage language, bool markChosen = true)
        {
            EnsureConfigured();
            Apply(language, save: true, markChosen);
            var handler = Changed;
            if (handler != null)
                handler(null, EventArgs.Empty);
        }

        public static string NativeName(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.Russian: return "Русский";
                case AppLanguage.English: return "English";
                case AppLanguage.Spanish: return "Español";
                case AppLanguage.German: return "Deutsch";
                case AppLanguage.French: return "Français";
                case AppLanguage.ChineseSimplified: return "简体中文";
                default: return language.ToString();
            }
        }

        /// <summary>Portable flag asset file name (PNG/SVG resolved by the host).</summary>
        public static string FlagImage(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.Russian: return "flag_ru.png";
                case AppLanguage.English: return "flag_gb.png";
                case AppLanguage.Spanish: return "flag_es.png";
                case AppLanguage.German: return "flag_de.png";
                case AppLanguage.French: return "flag_fr.png";
                case AppLanguage.ChineseSimplified: return "flag_cn.png";
                default: return "flag_ru.png";
            }
        }

        /// <summary>
        /// Chinese only: bilingual disclaimer (Chinese + English). Empty for other languages.
        /// </summary>
        public static string TranslationWarning(AppLanguage language)
        {
            if (language == AppLanguage.ChineseSimplified)
                return "警告：中文翻译可能不准确。\nWarning: the Chinese translation may be inaccurate.";
            return "";
        }

        public static string ToCultureName(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.English: return "en";
                case AppLanguage.Spanish: return "es";
                case AppLanguage.German: return "de";
                case AppLanguage.French: return "fr";
                case AppLanguage.ChineseSimplified: return "zh-Hans";
                default: return "ru";
            }
        }

        public static CultureInfo ToCulture(AppLanguage language) =>
            new CultureInfo(ToCultureName(language));

        public static AppLanguage[] AllLanguages { get; } =
        {
            AppLanguage.Russian,
            AppLanguage.English,
            AppLanguage.Spanish,
            AppLanguage.German,
            AppLanguage.French,
            AppLanguage.ChineseSimplified
        };

        private static void Apply(AppLanguage language, bool save, bool markChosen)
        {
            Current = language;
            if (_applyCulture != null)
                _applyCulture(language);

            if (save)
                _preferences.SetLanguage(language.ToString());
            if (markChosen)
                _preferences.SetChosen(true);
        }

        private static void EnsureConfigured()
        {
            if (_preferences == null)
                throw new InvalidOperationException(
                    "LanguageService.Configure must be called before Load/Set.");
        }

        private static bool TryParseLanguage(string raw, out AppLanguage language)
        {
            try
            {
                language = (AppLanguage)Enum.Parse(typeof(AppLanguage), raw, ignoreCase: true);
                return Enum.IsDefined(typeof(AppLanguage), language);
            }
            catch
            {
                language = AppLanguage.Russian;
                return false;
            }
        }
    }
}
