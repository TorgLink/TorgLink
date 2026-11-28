using Microsoft.Maui.Controls.Shapes;
using TorgLink.Localization;

namespace TorgLink.Maui.Controls;

/// <summary>Плавающая круглая кнопка «Сохранить» (иконка, правый нижний угол) поверх превью медиа.</summary>
public sealed class SaveMediaFab : Border
{
    public SaveMediaFab(Func<Task> onSave)
    {
        WidthRequest = 52;
        HeightRequest = 52;
        StrokeThickness = 0;
        StrokeShape = new RoundRectangle { CornerRadius = 26 };
        BackgroundColor = Color.FromArgb("#99000000");
        Padding = 0;
        Margin = new Thickness(0, 0, 20, 20);
        HorizontalOptions = LayoutOptions.End;
        VerticalOptions = LayoutOptions.End;
        Content = new Image
        {
            Source = "icon_save.png",
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        SemanticProperties.SetDescription(this, LocalizationUtils.GetStringByKey("save"));
        AutomationProperties.SetName(this, LocalizationUtils.GetStringByKey("save"));

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await onSave().ConfigureAwait(true);
        GestureRecognizers.Add(tap);
    }
}

