using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using TorgLink.Localization;

namespace TorgLink.Maui.Services;

/// <summary>
/// Контекстное меню строки чата: ПКМ (Windows ContextFlyout) / долгое нажатие (ActionSheet).
/// </summary>
internal static class ChatListContextMenu
{
    private static readonly BindableProperty WiredProperty =
        BindableProperty.CreateAttached("Wired", typeof(bool), typeof(ChatListContextMenu), false);

    private static readonly BindableProperty LongPressCtsProperty =
        BindableProperty.CreateAttached("LongPressCts", typeof(CancellationTokenSource), typeof(ChatListContextMenu),
            null);

    private static readonly BindableProperty SuppressTapProperty =
        BindableProperty.CreateAttached("SuppressTap", typeof(bool), typeof(ChatListContextMenu), false);

    public sealed class Deps
    {
        public required Page Host { get; init; }
        public required AuthService Auth { get; init; }
        public required PeerBlacklist Blacklist { get; init; }
        public required MessengerServerManager MessengerServers { get; init; }
        public required ChatRepository Chats { get; init; }
        public required UserP2pRuntime P2p { get; init; }
        public Func<Task>? AfterChange { get; init; }
        public Func<int, Task>? AfterDelete { get; init; }
    }

    public static bool ConsumeSuppressPrimaryTap(Element? start)
    {
        for (var el = start; el != null; el = el.Parent)
        {
            if (el is not BindableObject bo)
                continue;
            if (!(bool)bo.GetValue(SuppressTapProperty))
                continue;
            bo.SetValue(SuppressTapProperty, false);
            return true;
        }

        return false;
    }

    public static void EnsureWired(View rowRoot, Deps deps)
    {
        if (rowRoot.GetValue(WiredProperty) is true)
        {
            RefreshFlyout(rowRoot, deps);
            return;
        }

        rowRoot.SetValue(WiredProperty, true);
        rowRoot.BindingContextChanged += (_, _) => RefreshFlyout(rowRoot, deps);
        RefreshFlyout(rowRoot, deps);

        var secondary = new TapGestureRecognizer { Buttons = ButtonsMask.Secondary };
        secondary.Tapped += async (_, _) =>
        {
            var row = FindRow(rowRoot);
            if (row != null)
                await ShowActionSheetAsync(row, deps).ConfigureAwait(true);
        };
        rowRoot.GestureRecognizers.Add(secondary);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (_, _) =>
        {
            // Перед нативным ContextFlyout на desktop пересобрать пункты (block/unblock).
            if (IsDesktop())
                RefreshFlyout(rowRoot, deps);
            else
                StartLongPress(rowRoot, deps);
        };
        pointer.PointerReleased += (_, _) => CancelLongPress(rowRoot);
        pointer.PointerExited += (_, _) => CancelLongPress(rowRoot);
        rowRoot.GestureRecognizers.Add(pointer);
    }

    private static bool IsDesktop() =>
        DeviceInfo.Current.Platform == DevicePlatform.WinUI ||
        DeviceInfo.Current.Platform == DevicePlatform.MacCatalyst;

    public static ChatListRowVm? FindRow(Element? start)
    {
        for (var el = start; el != null; el = el.Parent)
        {
            if (el.BindingContext is ChatListRowVm vm)
                return vm;
        }

        return null;
    }

    private static void RefreshFlyout(View rowRoot, Deps deps)
    {
        if (rowRoot.BindingContext is not ChatListRowVm row)
        {
            FlyoutBase.SetContextFlyout(rowRoot, null);
            return;
        }

        // На Windows правый клик открывает нативное меню; тексты пересобираем при смене BindingContext/языка.
        var flyout = new MenuFlyout();
        var viewContact = new MenuFlyoutItem { Text = LocalizationUtils.GetStringByKey("chatmenu.view_contact") };
        viewContact.Clicked += async (_, _) => await ViewContactAsync(row, deps).ConfigureAwait(true);
        flyout.Add(viewContact);

        var u = deps.Auth.CurrentUser;
        var blocked = u != null && deps.Blacklist.IsBlocked(u.Id, row.PeerNetworkIdShort);
        var block = new MenuFlyoutItem
        {
            Text = LocalizationUtils.GetStringByKey(blocked ? "chatmenu.unblock" : "chatmenu.block")
        };
        block.Clicked += async (_, _) => await ToggleBlockAsync(row, deps).ConfigureAwait(true);
        flyout.Add(block);

        var delete = new MenuFlyoutItem { Text = LocalizationUtils.GetStringByKey("chatmenu.delete_chat") };
        delete.Clicked += async (_, _) => await DeleteChatAsync(row, deps).ConfigureAwait(true);
        flyout.Add(delete);

        FlyoutBase.SetContextFlyout(rowRoot, flyout);
    }

    private static void StartLongPress(View rowRoot, Deps deps)
    {
        CancelLongPress(rowRoot);
        var row = FindRow(rowRoot);
        if (row == null)
            return;

        // На Windows меню даёт ContextFlyout / Secondary; long-press оставляем для touch/Android/iOS.
        if (IsDesktop())
            return;

        var cts = new CancellationTokenSource();
        rowRoot.SetValue(LongPressCtsProperty, cts);
        _ = RunLongPressAsync(rowRoot, row, deps, cts.Token);
    }

    private static async Task RunLongPressAsync(View rowRoot, ChatListRowVm row, Deps deps,
        CancellationToken ct)
    {
        try
        {
            await Task.Delay(500, ct).ConfigureAwait(true);
            rowRoot.SetValue(SuppressTapProperty, true);
            await ShowActionSheetAsync(row, deps).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // released before threshold
        }
    }

    private static void CancelLongPress(View rowRoot)
    {
        if (rowRoot.GetValue(LongPressCtsProperty) is not CancellationTokenSource cts)
            return;
        rowRoot.SetValue(LongPressCtsProperty, null);
        try
        {
            cts.Cancel();
        }
        catch
        {
            // ignore
        }

        cts.Dispose();
    }

    public static async Task ShowActionSheetAsync(ChatListRowVm row, Deps deps)
    {
        var u = deps.Auth.CurrentUser;
        var blocked = u != null && deps.Blacklist.IsBlocked(u.Id, row.PeerNetworkIdShort);
        var view = LocalizationUtils.GetStringByKey("chatmenu.view_contact");
        var block = LocalizationUtils.GetStringByKey(blocked ? "chatmenu.unblock" : "chatmenu.block");
        var delete = LocalizationUtils.GetStringByKey("chatmenu.delete_chat");

        var action = await deps.Host.DisplayActionSheet(
            row.PeerNickname,
            LocalizationUtils.GetStringByKey("cancel"),
            delete,
            view,
            block).ConfigureAwait(true);

        if (string.IsNullOrEmpty(action) || action == LocalizationUtils.GetStringByKey("cancel"))
            return;
        if (action == view)
            await ViewContactAsync(row, deps).ConfigureAwait(true);
        else if (action == block)
            await ToggleBlockAsync(row, deps).ConfigureAwait(true);
        else if (action == delete)
            await DeleteChatAsync(row, deps).ConfigureAwait(true);
    }

    public static async Task ViewContactAsync(ChatListRowVm row, Deps deps)
    {
        var page = new ContactDetailsPage(row.PeerNickname, row.PeerNetworkIdShort, deps.P2p);
        await deps.Host.Navigation.PushAsync(page).ConfigureAwait(true);
    }

    public static async Task ToggleBlockAsync(ChatListRowVm row, Deps deps)
    {
        var u = deps.Auth.CurrentUser;
        if (u == null)
            return;

        await deps.Blacklist.EnsureLoadedAsync(u.Id).ConfigureAwait(true);
        if (deps.Blacklist.IsBlocked(u.Id, row.PeerNetworkIdShort))
        {
            await deps.Blacklist.RemoveAsync(u.Id, row.PeerNetworkIdShort).ConfigureAwait(true);
            if (deps.AfterChange != null)
                await deps.AfterChange().ConfigureAwait(true);
            return;
        }

        await BlacklistUi.ConfirmAndBlockAsync(
            deps.Host, deps.Blacklist, u.Id, row.PeerNetworkIdShort, row.PeerNickname,
            row.Chat.IsBot, deps.MessengerServers).ConfigureAwait(true);
        if (deps.AfterChange != null)
            await deps.AfterChange().ConfigureAwait(true);
    }

    public static async Task DeleteChatAsync(ChatListRowVm row, Deps deps)
    {
        var u = deps.Auth.CurrentUser;
        if (u == null)
            return;

        var chat = row.Chat;
        var confirm = await deps.Host.DisplayAlert(
            LocalizationUtils.GetStringByKey("chats.delete_title"),
            LocalizationUtils.GetStringByKeyWithFormat("chats.delete_body", chat.PeerNickname),
            LocalizationUtils.GetStringByKey("delete"),
            LocalizationUtils.GetStringByKey("cancel")).ConfigureAwait(true);
        if (!confirm)
            return;

        await deps.P2p.RemoveChatSessionAsync(chat.Id).ConfigureAwait(true);
        var deleted = await deps.Chats.DeleteChatAsync(chat.Id, u.Id).ConfigureAwait(true);
        if (deleted)
            AppLog.ChatDeleted(chat.Id);

        if (deps.AfterDelete != null)
            await deps.AfterDelete(chat.Id).ConfigureAwait(true);
        else if (deps.AfterChange != null)
            await deps.AfterChange().ConfigureAwait(true);
    }
}

