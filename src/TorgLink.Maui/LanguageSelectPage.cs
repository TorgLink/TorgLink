using TorgLink.Localization;

namespace TorgLink.Maui;

public sealed class LanguageSelectPage : ContentPage
{
    private AppLanguage _selected = AppLanguage.Russian;
    private readonly Label _warning = new()
    {
        FontSize = 13,
        TextColor = Color.FromArgb("#FF9500"),
        HorizontalTextAlignment = TextAlignment.Center,
        IsVisible = false
    };

    public LanguageSelectPage()
    {
        BackgroundColor = TorgLinkTheme.Current.PageBackground;
        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(28, 48),
            Spacing = 14,
            MaximumWidthRequest = 420,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        stack.Children.Add(new Image
        {
            Source = "logo_128.png",
            WidthRequest = 72,
            HeightRequest = 72,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center
        });
        stack.Children.Add(new Label
        {
            Text = "TorgLink",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = TorgLinkTheme.Accent,
            HorizontalOptions = LayoutOptions.Center
        });
        var title = new Label
        {
            Text = LocalizationUtils.GetStringByKey("lang.choose"),
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = TorgLinkTheme.Text,
            HorizontalOptions = LayoutOptions.Center
        };
        stack.Children.Add(title);

        foreach (var lang in new[]
                 {
                     AppLanguage.Russian, AppLanguage.English, AppLanguage.Spanish,
                     AppLanguage.German, AppLanguage.French, AppLanguage.ChineseSimplified
                 })
        {
            var button = new Button
            {
                Text = LanguageService.NativeName(lang),
                BackgroundColor = TorgLinkTheme.Current.Surface,
                TextColor = TorgLinkTheme.Text
            };
            var captured = lang;
            button.Clicked += (_, _) =>
            {
                _selected = captured;
                title.Text = LocalizationUtils.GetStringByKey("lang.choose"); // may still be previous culture until Set
                RefreshWarning();
                Highlight(stack, captured);
            };
            stack.Children.Add(button);
        }

        stack.Children.Add(_warning);
        var cont = new Button { Text = LocalizationUtils.GetStringByKey("lang.continue") };
        cont.Clicked += OnContinue;
        stack.Children.Add(cont);
        Content = new ScrollView { Content = stack };
        Highlight(stack, _selected);
        RefreshWarning();
    }

    private void RefreshWarning()
    {
        var warn = LanguageService.TranslationWarning(_selected);
        _warning.Text = warn;
        _warning.IsVisible = !string.IsNullOrEmpty(warn);
    }

    private static void Highlight(VerticalStackLayout stack, AppLanguage selected)
    {
        foreach (var child in stack.Children)
        {
            if (child is not Button b || b.Text is null)
                continue;
            var isLang = Enum.GetValues<AppLanguage>()
                .Any(l => string.Equals(LanguageService.NativeName(l), b.Text, StringComparison.Ordinal));
            if (!isLang)
                continue;
            var match = string.Equals(b.Text, LanguageService.NativeName(selected), StringComparison.Ordinal);
            b.BorderWidth = match ? 2 : 0;
            b.BorderColor = match ? TorgLinkTheme.Accent : Colors.Transparent;
        }
    }

    private void OnContinue(object? sender, EventArgs e)
    {
        LanguageService.Set(_selected);
        Application.Current!.MainPage =
            new NavigationPage(MauiProgram.Services.GetRequiredService<LoginPage>());
    }
}

