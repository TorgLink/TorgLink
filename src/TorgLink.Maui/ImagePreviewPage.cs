using TorgLink.Maui.Controls;
using TorgLink.Maui.Services;
using TorgLink.Localization;

namespace TorgLink.Maui;

/// <summary>Полноэкранный просмотр картинки из файла (байты грузятся только по клику).</summary>
public sealed class ImagePreviewPage : ContentPage
{
    private readonly string _filePath;
    private readonly string _displayName;
    private readonly DateTimeOffset? _receivedAt;

    public ImagePreviewPage(string filePath, string? displayName = null, DateTimeOffset? receivedAt = null)
    {
        _filePath = filePath;
        _displayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(filePath) : displayName;
        _receivedAt = receivedAt;
        Title = LocalizationUtils.GetStringByKey("image.title");
        BackgroundColor = Colors.Black;
        ToolbarItems.Add(new ToolbarItem
        {
            Text = LocalizationUtils.GetStringByKey("close"),
            Command = new Command(async () => await CloseAsync())
        });
        Content = new Grid
        {
            Children =
            {
                new Image
                {
                    Source = ImageSource.FromFile(filePath),
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    BackgroundColor = Colors.Black
                },
                new SaveMediaFab(() => SaveAsync())
            }
        };
    }

    private async Task SaveAsync()
    {
        try
        {
            await MediaFileSaver.SaveAsync(_filePath, _displayName, _receivedAt).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("save"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
    }

    private async Task CloseAsync()
    {
        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync().ConfigureAwait(true);
        else if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync().ConfigureAwait(true);
    }
}

