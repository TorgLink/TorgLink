using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Layouts;
using ShortP2P.Client.Data;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.TrustSystem;
using TorgLink.Localization;

namespace TorgLink.Maui;

public sealed class MessengerServersPage : ContentPage
{
    private readonly Entry _baseUrlEntry = new()
    {
        Placeholder = "https://host:7196",
        Keyboard = Keyboard.Url
    };

    private readonly Button _addButton = new();
    private readonly Button _importButton = new();
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };
    private readonly ObservableCollection<MessengerServerRowVm> _rows = [];
    private readonly MessengerServerManager _manager;
    private readonly ILogger<MessengerServersPage> _logger;
    private readonly Label _status = new() { FontSize = 12, TextColor = Colors.Gray };
    private readonly Label _introLabel = new() { FontSize = 12, TextColor = Colors.Gray };
    private readonly Label _baseUrlLabel = new();
    private readonly Label _activeHintLabel = new() { FontSize = 12, TextColor = Colors.Gray };
    private bool _suppressActiveToggle;

    public MessengerServersPage(MessengerServerManager manager, ILogger<MessengerServersPage> logger)
    {
        _manager = manager;
        _logger = logger;

        _list.ItemsSource = _rows;
        _addButton.Clicked += OnAddClicked;
        _importButton.Clicked += OnImportClicked;

        var root = new Grid
        {
            Padding = 16,
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        root.Add(_introLabel, 0, 0);
        root.Add(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _baseUrlLabel,
                _baseUrlEntry,
                _addButton,
                _importButton
            }
        }, 0, 1);
        root.Add(_status, 0, 2);
        root.Add(_list, 0, 3);
        root.Add(_activeHintLabel, 0, 4);
        Content = root;
        ApplyLocalizedUi();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedUi();
        UpdatePortraitDisplayMode();
        _manager.TrustThreatDetected -= OnTrustThreat;
        _manager.TrustThreatDetected += OnTrustThreat;
        await ReloadAsync().ConfigureAwait(true);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdatePortraitDisplayMode();
    }

    protected override void OnDisappearing()
    {
        _manager.TrustThreatDetected -= OnTrustThreat;
        base.OnDisappearing();
    }

    private void UpdatePortraitDisplayMode()
    {
        var isPortrait = Width > 0 && Height > 0 && Height > Width;
        foreach (var row in _rows)
            row.ApplyDisplayMode(isPortrait);
    }

    private void ApplyLocalizedUi()
    {
        Title = LocalizationUtils.GetStringByKey("servers.title");
        _introLabel.Text = LocalizationUtils.GetStringByKey("servers.intro");
        _baseUrlLabel.Text = LocalizationUtils.GetStringByKey("servers.base_url");
        _addButton.Text = LocalizationUtils.GetStringByKey("servers.add");
        _importButton.Text = LocalizationUtils.GetStringByKey("servers.import");
        _activeHintLabel.Text = LocalizationUtils.GetStringByKey("servers.active_hint");
        _list.ItemTemplate = CreateItemTemplate();
        foreach (var row in _rows)
            row.RefreshMeta();
    }

    private DataTemplate CreateItemTemplate() =>
        new(() =>
        {
            var url = new Label { FontSize = 16, LineBreakMode = LineBreakMode.WordWrap };
            url.SetBinding(Label.TextProperty, nameof(MessengerServerRowVm.DisplayBaseUrl));
            url.Triggers.Add(LowRatingColorTrigger());

            var meta = new Label { FontSize = 12, TextColor = Colors.Gray, LineBreakMode = LineBreakMode.WordWrap };
            meta.SetBinding(Label.TextProperty, nameof(MessengerServerRowVm.MetaLine));
            meta.Triggers.Add(LowRatingColorTrigger());

            var active = new Switch { VerticalOptions = LayoutOptions.Center };
            active.SetBinding(Switch.IsToggledProperty, new Binding(nameof(MessengerServerRowVm.Active),
                BindingMode.OneWay));
            active.Toggled += OnActiveToggled;

            var share = CreateRowActionButton(LocalizationUtils.GetStringByKey("servers.share"));
            share.Clicked += OnShareClicked;

            var recheck = CreateRowActionButton(LocalizationUtils.GetStringByKey("servers.check"));
            recheck.Clicked += OnRecheckClicked;

            var ask = CreateRowActionButton(LocalizationUtils.GetStringByKey("servers.ask"));
            ask.SetBinding(VisualElement.IsVisibleProperty, nameof(MessengerServerRowVm.CanAskServers));
            ask.Clicked += OnAskServersClicked;

            var delete = CreateRowActionButton(LocalizationUtils.GetStringByKey("servers.delete"));
            delete.BackgroundColor = Colors.DarkRed;
            delete.TextColor = Colors.White;
            delete.Clicked += OnDeleteClicked;

            var texts = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center,
                Children = { url, meta }
            };

            var header = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8,
                Children =
                {
                    texts,
                    active.AtColumn(1)
                }
            };

            var actions = new FlexLayout
            {
                Direction = FlexDirection.Row,
                Wrap = FlexWrap.Wrap,
                AlignItems = FlexAlignItems.Center,
                JustifyContent = FlexJustify.Start
            };
            actions.Children.Add(share);
            actions.Children.Add(recheck);
            actions.Children.Add(ask);
            actions.Children.Add(delete);

            return new VerticalStackLayout
            {
                Padding = new Thickness(0, 8),
                Spacing = 4,
                Children = { header, actions }
            };
        });

    private static Button CreateRowActionButton(string text) =>
        new()
        {
            Text = text,
            Padding = new Thickness(10, 4),
            Margin = new Thickness(0, 0, 8, 8),
            HorizontalOptions = LayoutOptions.Start
        };

    private static DataTrigger LowRatingColorTrigger() =>
        new(typeof(Label))
        {
            Binding = new Binding(nameof(MessengerServerRowVm.IsLowRating)),
            Value = true,
            Setters = { new Setter { Property = Label.TextColorProperty, Value = TorgLinkTheme.Danger } }
        };

    private void OnTrustThreat(object? sender, MessengerServerTrustThreatEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () => await ReloadAsync().ConfigureAwait(true));
    }

    private async Task ReloadAsync()
    {
        try
        {
            var servers = await _manager.ListAsync().ConfigureAwait(true);
            _suppressActiveToggle = true;
            _rows.Clear();
            foreach (var s in servers.OrderByDescending(x => x.UpdatedUtcTicks))
                _rows.Add(new MessengerServerRowVm(s));
            UpdatePortraitDisplayMode();
            _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.count", _rows.Count, MessengerServerLimits.MaxServersPerUser);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reload messenger servers");
            _status.Text = ex.Message;
        }
        finally
        {
            _suppressActiveToggle = false;
        }
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var url = _baseUrlEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(url))
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("error"), LocalizationUtils.GetStringByKey("servers.need_url"), LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        _addButton.IsEnabled = false;
        _status.Text = LocalizationUtils.GetStringByKey("servers.connecting");
        try
        {
            var entity = await _manager.AddServerAsync(url).ConfigureAwait(true);
            AppLog.ServerResponse("AddServer", entity.BaseUrl, $"id={entity.Id} trusted={entity.Trusted}");
            _baseUrlEntry.Text = "";
            _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.added", entity.BaseUrl);
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Add messenger server");
            await DisplayAlert(LocalizationUtils.GetStringByKey("error"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            _status.Text = ex.Message;
        }
        finally
        {
            _addButton.IsEnabled = true;
        }
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: MessengerServerRowVm row })
            return;

        if (!MessengerServerQrService.TryBuildPayload(row.BaseUrl, out var payload, out var err))
        {
            await DisplayAlert(LocalizationUtils.GetStringByKey("servers.share_title"), err ?? LocalizationUtils.GetStringByKey("servers.share_fail"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return;
        }

        try
        {
            var png = MessengerServerQrService.EncodeQrPng(payload);
            await Navigation.PushAsync(new MessengerServerQrPage(payload, png)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Share messenger server QR {Id}", row.Id);
            await DisplayAlert(LocalizationUtils.GetStringByKey("servers.share_title"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        FileResult? picked;
        try
        {
            picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = LocalizationUtils.GetStringByKey("servers.qr_picker"),
                FileTypes = FilePickerFileType.Images
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pick messenger server QR file");
            await DisplayAlert(LocalizationUtils.GetStringByKey("servers.import_title"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        if (picked == null)
            return;

        byte[] bytes;
        try
        {
            await using var stream = await picked.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            bytes = ms.ToArray();
            AppLog.BinaryLoaded("server-qr", picked.FileName, bytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Read messenger server QR file");
            await DisplayAlert(LocalizationUtils.GetStringByKey("servers.import_title"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        if (!MessengerServerQrService.TryDecodeImage(bytes, out var payload, out var err))
        {
            _logger.LogWarning("Messenger server QR decode failed from file: {Error}", err);
            await DisplayAlert(LocalizationUtils.GetStringByKey("servers.import_title"), err ?? LocalizationUtils.GetStringByKey("servers.import_read_fail"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return;
        }

        var url = MessengerServerQrCodec.ToBaseUrl(payload);
        _importButton.IsEnabled = false;
        _status.Text = LocalizationUtils.GetStringByKey("servers.importing");
        try
        {
            var existing = await _manager.FindExistingByEndpointAsync(url).ConfigureAwait(true);
            if (existing != null)
            {
                _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.already_status", existing.BaseUrl);
                await DisplayAlert(
                    LocalizationUtils.GetStringByKey("servers.import_title"),
                    LocalizationUtils.GetStringByKeyWithFormat("servers.already_body", existing.BaseUrl),
                    LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
                return;
            }

            var entity = await _manager.AddServerAsync(url).ConfigureAwait(true);
            AppLog.ServerResponse("ImportServer", entity.BaseUrl, $"id={entity.Id}");
            _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.imported", entity.BaseUrl);
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Import messenger server from QR");
            _status.Text = ex.Message;
            await DisplayAlert(LocalizationUtils.GetStringByKey("servers.import_title"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
        finally
        {
            _importButton.IsEnabled = true;
        }
    }

    private async void OnActiveToggled(object? sender, ToggledEventArgs e)
    {
        if (_suppressActiveToggle)
            return;
        if (sender is not BindableObject { BindingContext: MessengerServerRowVm row } sw)
            return;

        if (e.Value && !row.Trusted)
        {
            _suppressActiveToggle = true;
            if (sw is Switch switchControl)
                switchControl.IsToggled = false;
            _suppressActiveToggle = false;
            await DisplayAlert(
                LocalizationUtils.GetStringByKey("servers.untrusted_title"),
                LocalizationUtils.GetStringByKey("servers.untrusted_body"),
                LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
            return;
        }

        if (row.Active == e.Value)
            return;

        try
        {
            await _manager.SetActiveAsync(row.Id, e.Value).ConfigureAwait(true);
            AppLog.SettingChanged($"MessengerServer.Active:{row.BaseUrl}", e.Value);
            row.Active = e.Value;
            row.RefreshMeta();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetActive messenger server {Id}", row.Id);
            _suppressActiveToggle = true;
            if (sw is Switch switchControl)
                switchControl.IsToggled = !e.Value;
            _suppressActiveToggle = false;
            await DisplayAlert(LocalizationUtils.GetStringByKey("error"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
    }

    private async void OnAskServersClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: MessengerServerRowVm row } bindable)
            return;

        if (!row.CanAskServers)
            return;

        if (bindable is Button button)
            button.IsEnabled = false;
        _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.asking", row.BaseUrl);
        try
        {
            var result = await _manager.AskServersFromAsync(row.Id).ConfigureAwait(true);
            AppLog.ServerResponse("AskServers", row.BaseUrl,
                $"received={result.ReceivedCount} updated={result.UpdatedCount} added={result.AddedCount}");
            await ReloadAsync().ConfigureAwait(true);
            _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.ask_result", result.ReceivedCount, result.UpdatedCount, result.AddedCount,
                row.BaseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AskServers from messenger server {Id}", row.Id);
            _status.Text = ex.Message;
            await DisplayAlert(LocalizationUtils.GetStringByKey("servers.ask_title"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
        finally
        {
            if (bindable is Button restore)
                restore.IsEnabled = true;
        }
    }

    private async void OnRecheckClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: MessengerServerRowVm row } bindable)
            return;

        if (bindable is Button button)
            button.IsEnabled = false;
        _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.checking", row.BaseUrl);
        try
        {
            var result = await _manager.RecheckServerAsync(row.Id).ConfigureAwait(true);
            AppLog.ServerResponse("RecheckServer", result.Server.BaseUrl, result.Status);
            await ReloadAsync().ConfigureAwait(true);
            switch (result.Status)
            {
                case MessengerServerRecheckStatus.AvailableAndTrusted:
                    _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.available_status", result.Server.BaseUrl);
                    await DisplayAlert(
                        LocalizationUtils.GetStringByKey("servers.recheck_title"),
                        LocalizationUtils.GetStringByKey("servers.recheck_ok"),
                        LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
                    break;
                case MessengerServerRecheckStatus.Unreachable:
                    _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.unreachable_status", result.Server.BaseUrl);
                    await DisplayAlert(
                        LocalizationUtils.GetStringByKey("servers.recheck_title"),
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? LocalizationUtils.GetStringByKey("servers.recheck_unreachable")
                            : LocalizationUtils.GetStringByKeyWithFormat("servers.recheck_unreachable_detail", result.ErrorMessage),
                        LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
                    break;
                case MessengerServerRecheckStatus.FingerprintMismatch:
                    _status.Text = LocalizationUtils.GetStringByKeyWithFormat("servers.fp_status", result.Server.BaseUrl);
                    await DisplayAlert(
                        LocalizationUtils.GetStringByKey("servers.recheck_title"),
                        LocalizationUtils.GetStringByKeyWithFormat("servers.recheck_fp", result.ExpectedFingerprint, result.ActualFingerprint),
                        LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recheck messenger server {Id}", row.Id);
            _status.Text = ex.Message;
            await DisplayAlert(LocalizationUtils.GetStringByKey("error"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
        finally
        {
            if (bindable is Button restore)
                restore.IsEnabled = true;
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: MessengerServerRowVm row })
            return;

        var ok = await DisplayAlert(
            LocalizationUtils.GetStringByKey("servers.delete_title"),
            LocalizationUtils.GetStringByKeyWithFormat("servers.delete_body", row.BaseUrl),
            LocalizationUtils.GetStringByKey("servers.delete"),
            LocalizationUtils.GetStringByKey("cancel")).ConfigureAwait(true);
        if (!ok)
            return;

        try
        {
            await _manager.DeleteServerAsync(row.Id).ConfigureAwait(true);
            AppLog.SettingChanged($"MessengerServer.Deleted:{row.BaseUrl}", row.Id);
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Delete messenger server {Id}", row.Id);
            await DisplayAlert(LocalizationUtils.GetStringByKey("error"), ex.Message, LocalizationUtils.GetStringByKey("ok")).ConfigureAwait(true);
        }
    }

    private sealed class MessengerServerRowVm : INotifyPropertyChanged
    {
        private bool _active;
        private string _metaLine;
        private string _displayBaseUrl;

        public MessengerServerRowVm(MessengerServerEntity entity)
        {
            Id = entity.Id;
            BaseUrl = entity.BaseUrl;
            Trusted = entity.Trusted;
            _active = entity.Active;
            Fingerprint = entity.FingerprintSha256;
            IsRegistered = entity.IsRegistered;
            TrustRating = entity.TrustRating;
            _displayBaseUrl = BaseUrl;
            _metaLine = BuildMeta(
                entity.TrustRating, entity.Trusted, entity.Active, entity.IsRegistered, entity.FingerprintSha256);
        }

        public int Id { get; }
        public string BaseUrl { get; }
        public string DisplayBaseUrl
        {
            get => _displayBaseUrl;
            private set
            {
                if (_displayBaseUrl == value)
                    return;
                _displayBaseUrl = value;
                OnPropertyChanged();
            }
        }
        public bool Trusted { get; }
        public bool IsRegistered { get; }
        public string Fingerprint { get; }
        public float TrustRating { get; }
        public bool IsLowRating => TrustRating < TrustRatings.Floor;
        public bool CanAskServers => TrustRating >= TrustRatings.Floor;

        public bool Active
        {
            get => _active;
            set
            {
                if (_active == value)
                    return;
                _active = value;
                OnPropertyChanged();
            }
        }

        public string MetaLine
        {
            get => _metaLine;
            private set
            {
                if (_metaLine == value)
                    return;
                _metaLine = value;
                OnPropertyChanged();
            }
        }

        public void RefreshMeta() =>
            MetaLine = BuildMeta(TrustRating, Trusted, Active, IsRegistered, Fingerprint);

        public void ApplyDisplayMode(bool isPortrait)
        {
            DisplayBaseUrl = isPortrait ? CompactServerUrl(BaseUrl) : BaseUrl;
        }

        private static string CompactServerUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            var cleaned = value.Trim();
            if (!Uri.TryCreate(cleaned, UriKind.Absolute, out var uri))
                return cleaned;

            var host = uri.Host;
            if (string.IsNullOrWhiteSpace(host))
                return cleaned;

            if (host.Contains('.') && host.Split('.').Length == 4 && host.Split('.').All(part => int.TryParse(part, out _)))
            {
                var firstTwo = host.Split('.').Take(2);
                return string.Join('.', firstTwo) + ".....";
            }

            if (host.Contains('.'))
            {
                var first = host.Split('.')[0];
                return first + "....";
            }

            return host.Length <= 8 ? host : host[..8] + "....";
        }

        private static string BuildMeta(float rating, bool trusted, bool active, bool registered, string fp)
        {
            var shortFp = string.IsNullOrEmpty(fp)
                ? "—"
                : fp.Length <= 16
                    ? fp
                    : fp[..8] + "…" + fp[^8..];
            var trust = trusted ? LocalizationUtils.GetStringByKey("servers.meta_trusted") : LocalizationUtils.GetStringByKey("servers.meta_untrusted");
            var act = active ? LocalizationUtils.GetStringByKey("servers.meta_active") : LocalizationUtils.GetStringByKey("servers.meta_off");
            var reg = registered ? LocalizationUtils.GetStringByKey("servers.meta_registered") : LocalizationUtils.GetStringByKey("servers.meta_not_registered");
            var ratingText = rating.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            return LocalizationUtils.GetStringByKeyWithFormat("servers.meta", ratingText, trust, act, reg, shortFp);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

file static class MessengerServersPageViewExtensions
{
    public static T AtColumn<T>(this T view, int column) where T : View
    {
        Grid.SetColumn(view, column);
        return view;
    }
}

