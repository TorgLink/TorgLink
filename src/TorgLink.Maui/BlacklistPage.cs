using System.Collections.ObjectModel;
using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using TorgLink.Localization;

namespace TorgLink.Maui;

public sealed class BlacklistPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly PeerBlacklist _blacklist;
    private readonly ObservableCollection<PeerBlacklistEntity> _rows = [];
    private readonly CollectionView _list = new();

    public BlacklistPage(AuthService auth, PeerBlacklist blacklist)
    {
        _auth = auth;
        _blacklist = blacklist;
        _list.ItemsSource = _rows;
        _list.EmptyView = new Label
        {
            Margin = new Thickness(24),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var name = new Label { FontAttributes = FontAttributes.Bold, FontSize = 16 };
            name.SetBinding(Label.TextProperty, nameof(PeerBlacklistEntity.Nickname));
            var id = new Label { FontSize = 12 };
            id.SetBinding(Label.TextProperty, nameof(PeerBlacklistEntity.NetworkId));
            var un = new Button
            {
                FontSize = 13,
                Padding = new Thickness(10, 6),
                Text = LocalizationUtils.GetStringByKey("blacklist.unblock")
            };
            un.SetBinding(Button.CommandParameterProperty, ".");
            un.Clicked += OnUnblockClicked;
            var grid = new Grid
            {
                Padding = new Thickness(16, 10),
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                ColumnSpacing = 12
            };
            var texts = new VerticalStackLayout { Spacing = 2, Children = { name, id } };
            grid.Add(texts);
            Grid.SetColumn(un, 1);
            grid.Add(un);
            return grid;
        });

        Content = _list;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Title = LocalizationUtils.GetStringByKey("blacklist.title");
        if (_list.EmptyView is Label empty)
            empty.Text = LocalizationUtils.GetStringByKey("blacklist.empty");
        await ReloadAsync().ConfigureAwait(true);
    }

    private async Task ReloadAsync()
    {
        var u = _auth.CurrentUser;
        _rows.Clear();
        if (u == null)
            return;
        foreach (var row in await _blacklist.ListAsync(u.Id).ConfigureAwait(true))
        {
            if (string.IsNullOrWhiteSpace(row.Nickname))
                row.Nickname = row.NetworkId;
            _rows.Add(row);
        }
    }

    private async void OnUnblockClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: PeerBlacklistEntity row })
            return;
        var u = _auth.CurrentUser;
        if (u == null)
            return;
        await _blacklist.RemoveAsync(u.Id, row.NetworkId).ConfigureAwait(true);
        await ReloadAsync().ConfigureAwait(true);
    }
}

