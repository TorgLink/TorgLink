using ShortP2P.Auth.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using TorgLink.Localization;

namespace TorgLink.Maui;

public partial class AppHeaderView : ContentView
{
    private static readonly Color ServerWaiting = Color.FromArgb("#E6B422");
    private int _serverStatusEpoch;

    public AppHeaderView()
    {
        InitializeComponent();
        ApplyServerStatus(MessengerServerLinkStatus.Disabled);
        ApplySettingsAccessibility();
        SettingsIcon.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenSettingsAsync().ConfigureAwait(true))
        });
        var openProfile = new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenProfileAsync().ConfigureAwait(true))
        };
        SelfAvatarHost.GestureRecognizers.Add(openProfile);
        NickLabel.GestureRecognizers.Add(openProfile);
    }

    public void Bind(UserEntity? user, UserP2pRuntime p2p)
    {
        ApplySettingsAccessibility();
        NickLabel.Text = user?.Nickname ?? "";
        PortLabel.Text = user == null ? "" : LocalizationUtils.GetStringByKeyWithFormat("header.port", user.DataUdpPort);
        if (user == null)
        {
            SelfAvatarHost.IsVisible = false;
        }
        else
        {
            SelfAvatarHost.IsVisible = true;
            AvatarBadge.Apply(SelfAvatarFill, SelfAvatarInitials, SelfAvatarImage, user.Nickname,
                user.NetworkIdShort, user.Avatar);
        }

        var meshOn = p2p.LocalScan.IsUdpListening || p2p.Settings.EnableUdpTransport;
        MeshDot.Fill = meshOn ? TorgLinkTheme.Online : TorgLinkTheme.Offline;
        MeshLabel.Text = meshOn ? LocalizationUtils.GetStringByKey("header.mesh_on") : LocalizationUtils.GetStringByKey("header.mesh_off");
        MeshLabel.TextColor = meshOn ? TorgLinkTheme.Online : TorgLinkTheme.Muted;
        var btOn = p2p.Settings.EnableBluetoothTransport && p2p.LocalScan.IsBluetoothListening;
        BtIcon.Opacity = btOn ? 1 : 0.28;
        _ = RefreshServerStatusAsync(p2p);
    }

    private void ApplySettingsAccessibility()
    {
        var text = LocalizationUtils.GetStringByKey("tab.settings");
        ToolTipProperties.SetText(SettingsIcon, text);
        SemanticProperties.SetDescription(SettingsIcon, text);
        AutomationProperties.SetName(SettingsIcon, text);
        var profile = LocalizationUtils.GetStringByKey("profile.title");
        ToolTipProperties.SetText(SelfAvatarHost, profile);
        SemanticProperties.SetDescription(SelfAvatarHost, profile);
    }

    private static async Task OpenSettingsAsync()
    {
        var current = Shell.Current?.CurrentPage;
        if (current is null or SettingsPage)
            return;
        var page = MauiProgram.Services.GetRequiredService<SettingsPage>();
        await current.Navigation.PushAsync(page).ConfigureAwait(true);
    }

    private static async Task OpenProfileAsync()
    {
        var current = Shell.Current?.CurrentPage;
        if (current is null or ProfilePage)
            return;
        var page = MauiProgram.Services.GetRequiredService<ProfilePage>();
        await current.Navigation.PushAsync(page).ConfigureAwait(true);
    }

    private async Task RefreshServerStatusAsync(UserP2pRuntime p2p)
    {
        var epoch = Interlocked.Increment(ref _serverStatusEpoch);
        MessengerServerLinkStatus status;
        try
        {
            var manager = p2p.MessengerServers?.Manager;
            status = manager == null
                ? MessengerServerLinkStatus.Disabled
                : await manager.GetLinkStatusAsync().ConfigureAwait(false);
        }
        catch
        {
            status = MessengerServerLinkStatus.Disconnected;
        }

        if (epoch != Volatile.Read(ref _serverStatusEpoch))
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (epoch != Volatile.Read(ref _serverStatusEpoch))
                return;
            ApplyServerStatus(status);
        }).ConfigureAwait(false);
    }

    private void ApplyServerStatus(MessengerServerLinkStatus status)
    {
        switch (status)
        {
            case MessengerServerLinkStatus.Connected:
                ServerDot.Fill = TorgLinkTheme.Online;
                ServerLabel.Text = LocalizationUtils.GetStringByKey("header.server_on");
                ServerLabel.TextColor = TorgLinkTheme.Online;
                break;
            case MessengerServerLinkStatus.Waiting:
                ServerDot.Fill = ServerWaiting;
                ServerLabel.Text = LocalizationUtils.GetStringByKey("header.server_wait");
                ServerLabel.TextColor = ServerWaiting;
                break;
            case MessengerServerLinkStatus.Disconnected:
                ServerDot.Fill = TorgLinkTheme.Danger;
                ServerLabel.Text = LocalizationUtils.GetStringByKey("header.server_fail");
                ServerLabel.TextColor = TorgLinkTheme.Danger;
                break;
            default:
                ServerDot.Fill = TorgLinkTheme.Offline;
                ServerLabel.Text = LocalizationUtils.GetStringByKey("header.server_off");
                ServerLabel.TextColor = TorgLinkTheme.Muted;
                break;
        }
    }
}

