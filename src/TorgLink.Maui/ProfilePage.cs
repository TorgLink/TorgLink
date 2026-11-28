using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Services;
using TorgLink.Localization;

namespace TorgLink.Maui;

/// <summary>Own profile editor: AboutMe + avatar (512×512 square crop, circular UI clip).</summary>
public sealed class ProfilePage : ContentPage
{
    private readonly AuthService _auth;
    private readonly UserP2pRuntime _p2p;
    private readonly ILogger<ProfilePage> _logger;
    private readonly Editor _aboutMe;
    private readonly Label _aboutCounter;
    private readonly Border _avatarFill;
    private readonly Label _avatarInitials;
    private readonly Image _avatarImage;
    private readonly Label _status;
    private byte[]? _avatarBytes;

    public ProfilePage(AuthService auth, UserP2pRuntime p2p, ILogger<ProfilePage> logger)
    {
        _auth = auth;
        _p2p = p2p;
        _logger = logger;
        Title = LocalizationUtils.GetStringByKey("profile.title");
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        SetDynamicResource(BackgroundColorProperty, "PageBackground");

        _avatarFill = new Border
        {
            WidthRequest = 96,
            HeightRequest = 96,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 48 },
            HorizontalOptions = LayoutOptions.Center
        };
        _avatarInitials = new Label
        {
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

        _aboutMe = new Editor
        {
            AutoSize = EditorAutoSizeOption.TextChanges,
            HeightRequest = 120,
            Placeholder = LocalizationUtils.GetStringByKeyWithFormat("profile.about_ph", PeerProfileLimits.MaxAboutMeChars)
        };
        _aboutMe.SetDynamicResource(Editor.TextColorProperty, "MidnightBlue");
        _aboutCounter = new Label { FontSize = 12 };
        _aboutCounter.SetDynamicResource(Label.TextColorProperty, "MutedText");
        _status = new Label { FontSize = 12 };
        _status.SetDynamicResource(Label.TextColorProperty, "MutedText");

        var pick = new Button { Text = LocalizationUtils.GetStringByKey("profile.choose_avatar") };
        var clear = new Button { Text = LocalizationUtils.GetStringByKey("profile.clear_avatar") };
        var save = new Button { Text = LocalizationUtils.GetStringByKey("profile.save") };
        pick.Clicked += async (_, _) => await OnPickAvatarAsync().ConfigureAwait(true);
        clear.Clicked += (_, _) =>
        {
            _avatarBytes = null;
            RefreshAvatarPreview();
        };
        save.Clicked += async (_, _) => await OnSaveAsync().ConfigureAwait(true);
        _aboutMe.TextChanged += (_, _) => UpdateCounter();

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
            Text = LocalizationUtils.GetStringByKey("profile.title"),
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };
        title.SetDynamicResource(Label.TextColorProperty, "MidnightBlue");
        var header = new Grid
        {
            Padding = new Thickness(12, 8, 16, 8),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            Children = { back, title }
        };
        Grid.SetColumn(title, 1);

        var hint = new Label
        {
            Text = LocalizationUtils.GetStringByKeyWithFormat("profile.avatar_hint", PeerProfileLimits.MaxAvatarBytes / 1024, AvatarImageProcessor.Dimension),
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        };
        hint.SetDynamicResource(Label.TextColorProperty, "MutedText");

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
                        Spacing = 12,
                        Children =
                        {
                            avatarHost,
                            pick,
                            clear,
                            hint,
                            _aboutMe,
                            _aboutCounter,
                            save,
                            _status
                        }
                    }
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Title = LocalizationUtils.GetStringByKey("profile.title");
        LoadCurrentProfileBestEffort();
    }

    private void LoadCurrentProfileBestEffort()
    {
        try
        {
            var user = _auth.CurrentUser;
            if (user == null)
                return;
            _aboutMe.Text = user.AboutMe ?? "";
            _avatarBytes = user.Avatar;
            RefreshAvatarPreview();
            UpdateCounter();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load own profile into UI (best-effort); continuing");
            _aboutMe.Text = "";
            _avatarBytes = null;
            RefreshAvatarPreview();
            UpdateCounter();
        }
    }

    private void UpdateCounter()
    {
        var len = _aboutMe.Text?.Length ?? 0;
        _aboutCounter.Text = $"{len} / {PeerProfileLimits.MaxAboutMeChars}";
    }

    private void RefreshAvatarPreview()
    {
        var user = _auth.CurrentUser;
        var nick = user?.Nickname ?? "?";
        var key = user?.NetworkIdShort ?? nick;
        AvatarBadge.Apply(_avatarFill, _avatarInitials, _avatarImage, nick, key, _avatarBytes);
    }

    private async Task OnPickAvatarAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = LocalizationUtils.GetStringByKey("profile.choose_avatar"),
                FileTypes = FilePickerFileType.Images
            }).ConfigureAwait(true);
            if (result == null)
                return;

            await using var stream = await result.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            var raw = ms.ToArray();
            var prepared = AvatarImageProcessor.PrepareCropped(raw);
            if (prepared.Status == AvatarImageProcessor.PrepareStatus.Failed)
            {
                _status.Text = prepared.Error ?? LocalizationUtils.GetStringByKey("profile.avatar_failed");
                return;
            }

            byte[]? bytes = prepared.Bytes;
            if (prepared.Status == AvatarImageProcessor.PrepareStatus.NeedsCompression)
            {
                var sizeKb = Math.Max(1, (prepared.SizeAfterCrop + 1023) / 1024);
                var limitKb = PeerProfileLimits.MaxAvatarBytes / 1024;
                var compress = await DisplayAlert(
                    LocalizationUtils.GetStringByKey("profile.compress_title"),
                    LocalizationUtils.GetStringByKeyWithFormat("profile.compress_body", sizeKb, limitKb),
                    LocalizationUtils.GetStringByKey("profile.compress_yes"),
                    LocalizationUtils.GetStringByKey("cancel")).ConfigureAwait(true);
                if (!compress)
                {
                    _status.Text = LocalizationUtils.GetStringByKeyWithFormat("profile.avatar_too_large", limitKb);
                    return;
                }

                if (!AvatarImageProcessor.TryCompressToLimit(prepared.Bytes!, PeerProfileLimits.MaxAvatarBytes,
                        out bytes, out var err))
                {
                    _status.Text = err ?? LocalizationUtils.GetStringByKeyWithFormat("profile.avatar_too_large", limitKb);
                    return;
                }
            }

            _avatarBytes = bytes;
            RefreshAvatarPreview();
            _status.Text = "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pick avatar (best-effort)");
            _status.Text = LocalizationUtils.GetStringByKey("profile.avatar_failed");
        }
    }

    private async Task OnSaveAsync()
    {
        try
        {
            var text = _aboutMe.Text ?? "";
            if (text.Length > PeerProfileLimits.MaxAboutMeChars)
                text = text[..PeerProfileLimits.MaxAboutMeChars];

            var (ok, error) = await _auth.UpdateProfileAsync(text, _avatarBytes).ConfigureAwait(true);
            if (!ok)
            {
                _status.Text = error ?? LocalizationUtils.GetStringByKey("profile.save_failed");
                return;
            }

            _ = _p2p.BroadcastLocalUserInfoToContactsAsync();
            _status.Text = LocalizationUtils.GetStringByKey("profile.saved");
            await Navigation.PopAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save own profile");
            _status.Text = LocalizationUtils.GetStringByKey("profile.save_failed");
        }
    }
}

