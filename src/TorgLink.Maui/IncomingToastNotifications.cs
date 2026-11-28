using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Services;
using TorgLink.Localization;
#if WINDOWS
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
#endif

namespace TorgLink.Maui;

/// <summary>
/// Tray / Action Center notifications (Windows).
/// Classic ToastNotificationManager + Start Menu shortcut with AppUserModelID.
/// WinAppSDK AppNotificationManager is unreliable for unpackaged self-contained MAUI.
/// </summary>
internal static class IncomingToastNotifications
{
#if WINDOWS
    private const string Aumid = "com.torglink.maui";
    private const string ShortcutFileName = "TorgLink.lnk";
#endif

    private static int _hooked;
    private static ILogger? _logger;

    public static void EnsureHooked(ChatRepository repo, AuthService auth, PeerBlacklist blacklist, ILogger logger)
    {
        if (Interlocked.Exchange(ref _hooked, 1) != 0)
            return;

        _logger = logger;
#if WINDOWS
        TryRegisterWindows();
#endif

        repo.ChatMessageAppended += (_, e) =>
        {
            if (e.Outgoing)
                return;
            _ = ShowIncomingAsync(repo, auth, blacklist, e.ChatId, isNewChat: false);
        };

        repo.ChatCreated += (_, e) =>
        {
            if (!e.Remote)
                return;
            _ = ShowIncomingAsync(repo, auth, blacklist, e.ChatId, isNewChat: true);
        };
    }

    private static async Task ShowIncomingAsync(
        ChatRepository repo,
        AuthService auth,
        PeerBlacklist blacklist,
        int chatId,
        bool isNewChat)
    {
        try
        {
            if (ActiveChatTracker.IsViewing(chatId))
                return;

            var user = auth.CurrentUser;
            if (user != null)
                await blacklist.EnsureLoadedAsync(user.Id).ConfigureAwait(false);

            var chat = await repo.GetChatAsync(chatId).ConfigureAwait(false);
            if (chat == null)
                return;
            if (blacklist.IsBlocked(user?.Id, chat.PeerNetworkIdShort))
                return;

            string title;
            string body;
            if (isNewChat)
            {
                title = LocalizationUtils.GetStringByKey("notify.new_chat");
                var nick = string.IsNullOrWhiteSpace(chat.PeerNickname)
                    ? chat.PeerNetworkIdShort
                    : chat.PeerNickname;
                body = LocalizationUtils.GetStringByKeyWithFormat("notify.new_chat_body", nick);
            }
            else
            {
                title = string.IsNullOrWhiteSpace(chat.PeerNickname)
                    ? LocalizationUtils.GetStringByKey("notify.new_message")
                    : chat.PeerNickname;
                var last = (await repo.ListMessagesPageDescAsync(chatId, 0, 1, includePayloadBlob: false)
                    .ConfigureAwait(false))
                    .FirstOrDefault();
                body = ChatNav.Preview(last);
            }

            ShowToast(chatId, title, body);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Incoming toast failed (chat {ChatId})", chatId);
        }
    }

    private static void ShowToast(int chatId, string title, string body)
    {
#if WINDOWS
        try
        {
            void Show()
            {
                EnsureRegistered();
                var xml =
                    "<toast launch=\"action=openChat;chatId=" +
                    chatId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    "\">" +
                    "<visual><binding template=\"ToastGeneric\">" +
                    "<text>" + EscapeXml(Truncate(title, 80)) + "</text>" +
                    "<text>" + EscapeXml(Truncate(body, 200)) + "</text>" +
                    "</binding></visual>" +
                    "<audio silent=\"true\"/>" +
                    "</toast>";

                var doc = new XmlDocument();
                doc.LoadXml(xml);
                var toast = new ToastNotification(doc)
                {
                    Tag = "chat-" + chatId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Group = "torglink",
                    ExpirationTime = DateTimeOffset.Now.AddHours(12)
                };
                ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
                _logger?.LogInformation("Toast shown for chat {ChatId}: {Title}", chatId, title);
            }

            if (MainThread.IsMainThread)
                Show();
            else
                MainThread.BeginInvokeOnMainThread(Show);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ToastNotification.Show failed");
        }
#else
        _ = (chatId, title, body);
#endif
    }

#if WINDOWS
    private static void TryRegisterWindows()
    {
        try
        {
            EnsureRegistered();
            _logger?.LogInformation("Classic toast ready (AUMID {Aumid})", Aumid);

            // One-shot probe so Action Center wiring is obvious after upgrade.
            if (!Preferences.Default.Get("toast_probe_v2", false))
            {
                Preferences.Default.Set("toast_probe_v2", true);
                ShowToast(0, "TorgLink", LocalizationUtils.GetStringByKey("notify.new_message"));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Classic toast registration failed");
        }
    }

    private static void EnsureRegistered()
    {
        SetCurrentProcessExplicitAppUserModelID(Aumid);
        EnsureStartMenuShortcut();
    }

    private static void EnsureStartMenuShortcut()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        Directory.CreateDirectory(programs);
        var shortcutPath = Path.Combine(programs, ShortcutFileName);

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath))
            throw new InvalidOperationException("Cannot resolve process path for toast shortcut.");

        if (File.Exists(shortcutPath))
        {
            try
            {
                if (string.Equals(GetShortcutTarget(shortcutPath), exePath, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            catch
            {
                // recreate
            }
        }

        CreateShortcut(shortcutPath, exePath, Aumid, "TorgLink");
        _logger?.LogInformation("Start Menu shortcut for toasts: {Path}", shortcutPath);
    }

    private static string GetShortcutTarget(string shortcutPath)
    {
        var link = (IShellLinkW)new CShellLink();
        ((IPersistFile)link).Load(shortcutPath, 0);
        var sb = new StringBuilder(260);
        link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
        return sb.ToString();
    }

    private static void CreateShortcut(string shortcutPath, string exePath, string aumid, string description)
    {
        var link = (IShellLinkW)new CShellLink();
        link.SetPath(exePath);
        link.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? "");
        link.SetDescription(description);

        var store = (IPropertyStore)link;
        var key = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        var pv = PropVariant.FromString(aumid);
        store.SetValue(ref key, ref pv);
        store.Commit();
        PropVariant.Clear(ref pv);

        ((IPersistFile)link).Save(shortcutPath, true);
    }

    private static Page? ResolveHostPage()
    {
        var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
        if (page is Shell shell)
            return shell.CurrentPage ?? shell;
        return page;
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value ?? "";
        return value.Substring(0, max - 1) + "…";
    }

    private static string EscapeXml(string value) =>
        System.Security.SecurityElement.Escape(value) ?? "";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, out PropVariant pv);
        void SetValue(ref PropertyKey key, ref PropVariant pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;

        public PropertyKey(Guid formatId, uint propertyId)
        {
            fmtid = formatId;
            pid = propertyId;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort vt;
        public ushort w1;
        public ushort w2;
        public ushort w3;
        public IntPtr p;

        public static PropVariant FromString(string value) => new()
        {
            vt = 31, // VT_LPWSTR
            p = Marshal.StringToCoTaskMemUni(value)
        };

        public static void Clear(ref PropVariant pv)
        {
            PropVariantClear(ref pv);
            pv = default;
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);
    }
#endif
}

