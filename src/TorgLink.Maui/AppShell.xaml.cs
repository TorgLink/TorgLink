using TorgLink.Localization;

namespace TorgLink.Maui;

public partial class AppShell : Shell
{
    public AppShell(ChatsPage chats, ContactsPage contacts, NetworkPage network)
    {
        InitializeComponent();
        ChatsHost.Content = chats;
        ContactsHost.Content = contacts;
        NetworkHost.Content = network;
        ApplyChrome();
        ApplyLocalizedTitles();
        ThemeService.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(ApplyChrome);
        LanguageService.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(ApplyLocalizedTitles);
    }

    private void ApplyChrome()
    {
        var p = TorgLinkTheme.Current;
        BackgroundColor = p.PageBackground;
        FlyoutBackgroundColor = p.PageBackground;
        SetValue(TabBarBackgroundColorProperty, p.Surface);
        SetValue(TabBarForegroundColorProperty, p.Accent);
        SetValue(TabBarTitleColorProperty, p.Accent);
        SetValue(TabBarUnselectedColorProperty, p.Muted);
        SetValue(ForegroundColorProperty, p.TextPrimary);
        SetValue(TitleColorProperty, p.TextPrimary);
    }

    private void ApplyLocalizedTitles()
    {
        var chats = LocalizationUtils.GetStringByKey("tab.chats");
        var contacts = LocalizationUtils.GetStringByKey("tab.contacts");
        var network = LocalizationUtils.GetStringByKey("tab.network");

        // TabBar reads Tab.Title (not ShellContent). Named tabs so we never miss the section.
        ChatsTab.Title = chats;
        ContactsTab.Title = contacts;
        NetworkTab.Title = network;

        ChatsHost.Title = chats;
        ContactsHost.Title = contacts;
        NetworkHost.Title = network;

        SetPageTitle(ChatsHost.Content, chats);
        SetPageTitle(ContactsHost.Content, contacts);
        SetPageTitle(NetworkHost.Content, network);

        // Fallback walk for platforms that flatten / wrap TabBar differently
        foreach (var item in Items)
            ApplyTitlesToItem(item, chats, contacts, network);
    }

    private static void SetPageTitle(object? content, string title)
    {
        if (content is Page page)
            page.Title = title;
    }

    private static void ApplyTitlesToItem(ShellItem item, string chats, string contacts, string network)
    {
        foreach (var section in item.Items)
            ApplyTitlesToSection(section, chats, contacts, network);
    }

    private static void ApplyTitlesToSection(ShellSection section, string chats, string contacts, string network)
    {
        var title = MatchTabTitle(section, chats, contacts, network);
        if (title != null)
            section.Title = title;

        foreach (var content in section.Items)
        {
            var contentTitle = MatchTabTitle(content, chats, contacts, network) ?? title;
            if (contentTitle != null)
                content.Title = contentTitle;
        }
    }

    private static string? MatchTabTitle(BaseShellItem item, string chats, string contacts, string network)
    {
        // Match by route/name when available; otherwise by known RU/EN/ES/ZH titles already set.
        var key = item.Route ?? item.Title ?? "";
        if (ContainsAny(key, "chat", "чат", "聊天"))
            return chats;
        if (ContainsAny(key, "contact", "контакт", "联系"))
            return contacts;
        if (ContainsAny(key, "network", "сеть", "red", "网络"))
            return network;
        return null;
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

