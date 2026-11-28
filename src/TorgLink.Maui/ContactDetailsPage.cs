using Microsoft.Extensions.Logging;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using TorgLink.Localization;

namespace TorgLink.Maui;

/// <summary>Просмотр контакта: ник, Network ID, AboutMe и аватар из локального кэша профиля.</summary>
public sealed class ContactDetailsPage : ContentPage
{
    private readonly Border _avatarFill;
    private readonly Label _avatarInitials;
    private readonly Image _avatarImage;
    private readonly Label _aboutValue;
    private readonly string _nickname;
    private readonly string _networkIdShort;
    private readonly UserP2pRuntime? _p2p;
    private readonly ILogger? _logger;

    public ContactDetailsPage(string nickname, string networkIdShort, UserP2pRuntime? p2p = null,
        ILogger? logger = null)
    {
        _nickname = string.IsNullOrWhiteSpace(nickname) ? networkIdShort : nickname.Trim();
        _networkIdShort = networkIdShort?.Trim() ?? "";
        _p2p = p2p;
        _logger = logger;

        Title = LocalizationUtils.GetStringByKey("contact.title");
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        SetDynamicResource(BackgroundColorProperty, "PageBackground");

        var back = new Button
        {
            Text = "\u2039",
            FontSize = 28,
            BackgroundColor = Colors.Transparent,
            Padding = 0,
            WidthRequest = 36,
            HeightRequest = 40
        };
        back.SetDynamicResource(Button.TextColorProperty, "MidnightBlue");
        back.Clicked += async (_, _) =>
        {
            if (Navigation.NavigationStack.Count > 1)
                await Navigation.PopAsync().ConfigureAwait(true);
        };

        var title = new Label
        {
            Text = LocalizationUtils.GetStringByKey("contact.title"),
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };
        title.SetDynamicResource(Label.TextColorProperty, "MidnightBlue");

        var header = new Grid
        {
            Padding = new Thickness(12, 8, 16, 8),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = 4,
            Children = { back, title }
        };
        Grid.SetColumn(title, 1);

        _avatarFill = new Border
        {
            WidthRequest = 96,
            HeightRequest = 96,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 48 },
            BackgroundColor = TorgLinkTheme.AvatarColor(_networkIdShort),
            HorizontalOptions = LayoutOptions.Center
        };
        _avatarInitials = new Label
        {
            Text = TorgLinkTheme.Initials(_nickname),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 32,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        _avatarImage = new Image
        {
            WidthRequest = 96,
            HeightRequest = 96,
            Aspect = Aspect.AspectFill,
            IsVisible = false
        };
        var avatarHost = new Grid
        {
            WidthRequest = 96,
            HeightRequest = 96,
            HorizontalOptions = LayoutOptions.Center,
            Children = { _avatarFill, _avatarInitials, _avatarImage }
        };

        var nickLabel = FieldLabel(LocalizationUtils.GetStringByKey("contact.nickname"));
        var nickValue = FieldValue(_nickname);
        var idLabel = FieldLabel(LocalizationUtils.GetStringByKey("contact.network_id"));
        var idValue = FieldValue(_networkIdShort);
        var aboutLabel = FieldLabel(LocalizationUtils.GetStringByKey("contact.about"));
        _aboutValue = FieldValue(LocalizationUtils.GetStringByKey("contact.not_set"));
        _aboutValue.TextColor = TorgLinkTheme.Muted;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(0, 0, 0, 24),
                Spacing = 0,
                Children =
                {
                    header,
                    new VerticalStackLayout
                    {
                        Padding = new Thickness(24, 16, 24, 8),
                        Spacing = 16,
                        Children =
                        {
                            avatarHost,
                            nickLabel,
                            nickValue,
                            idLabel,
                            idValue,
                            aboutLabel,
                            _aboutValue
                        }
                    }
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadPeerProfileBestEffortAsync();
    }

    private async Task LoadPeerProfileBestEffortAsync()
    {
        var store = _p2p?.PeerProfiles;
        if (store == null || string.IsNullOrWhiteSpace(_networkIdShort))
            return;

        try
        {
            var id = CompressedNetworkId.FromShortString(_networkIdShort);
            var snap = await store.GetAsync(id).ConfigureAwait(true);
            if (snap == null)
                return;

            var nick = string.IsNullOrWhiteSpace(snap.Nickname) ? _nickname : snap.Nickname.Trim();
            byte[]? avatar = snap.Avatar;
            if (avatar is { Length: > PeerProfileLimits.MaxAvatarDisplayBytes })
            {
                _logger?.LogWarning(
                    "Peer avatar for {NetworkId} is {Bytes} bytes (max {Max}); ignoring oversized blob",
                    _networkIdShort, avatar.Length, PeerProfileLimits.MaxAvatarDisplayBytes);
                avatar = null;
            }

            AvatarBadge.Apply(_avatarFill, _avatarInitials, _avatarImage, nick, _networkIdShort, avatar);
            if (!string.IsNullOrWhiteSpace(snap.AboutMe))
            {
                _aboutValue.Text = snap.AboutMe.Trim();
                _aboutValue.SetDynamicResource(Label.TextColorProperty, "MidnightBlue");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Failed to load peer profile for {NetworkId} (best-effort); continuing",
                _networkIdShort);
        }
    }

    private static Label FieldLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        label.SetDynamicResource(Label.TextColorProperty, "MutedText");
        return label;
    }

    private static Label FieldValue(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 17,
            LineBreakMode = LineBreakMode.WordWrap
        };
        label.SetDynamicResource(Label.TextColorProperty, "MidnightBlue");
        return label;
    }
}

