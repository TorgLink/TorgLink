using TorgLink.Localization;

namespace TorgLink.WinForms.Localization;

internal static class FlagImages
{
    public static Image? Load(AppLanguage language)
    {
        var file = LanguageService.FlagImage(language); // flag_xx.png
        var resource = "TorgLink.WinForms.Resources." + file;
        var asm = typeof(FlagImages).Assembly;
        using var stream = asm.GetManifestResourceStream(resource);
        if (stream == null)
            return null;
        return new Bitmap(stream);
    }

    public static string ResourceFileName(AppLanguage language) =>
        LanguageService.FlagImage(language);
}
