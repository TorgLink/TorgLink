using System.Runtime.InteropServices;

namespace TorgLink.WinForms;

/// <summary>
/// Minimal Simple MAPI helper for attaching a file to the default mail client (Win7+).
/// </summary>
internal static class SimpleMapiMail
{
    private const int MapiLogonUi = 0x00000001;
    private const int MapiDialog = 0x00000008;

    public static bool TrySendWithAttachment(
        IWin32Window owner,
        string subject,
        string body,
        string attachmentPath,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(attachmentPath) || !File.Exists(attachmentPath))
        {
            error = "File not found.";
            return false;
        }

        var file = new MapiFileDescW
        {
            reserved = 0,
            flags = 0,
            position = unchecked((uint)-1),
            pathName = Path.GetFullPath(attachmentPath),
            fileName = Path.GetFileName(attachmentPath),
            fileType = IntPtr.Zero
        };
        var filePtr = IntPtr.Zero;
        try
        {
            filePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MapiFileDescW)));
            Marshal.StructureToPtr(file, filePtr, false);

            var message = new MapiMessageW
            {
                reserved = 0,
                subject = subject ?? "",
                noteText = body ?? "",
                messageType = null,
                dateReceived = null,
                conversationID = null,
                flags = 0,
                originator = IntPtr.Zero,
                recipCount = 0,
                recips = IntPtr.Zero,
                fileCount = 1,
                files = filePtr
            };

            var hwnd = owner?.Handle ?? IntPtr.Zero;
            var rc = MAPISendMailW(IntPtr.Zero, hwnd, ref message, MapiLogonUi | MapiDialog, 0);
            // 0 = success, 1 = user aborted — both OK for our purposes.
            if (rc == 0 || rc == 1)
                return true;

            error = $"MAPI error {rc}.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (filePtr != IntPtr.Zero)
                Marshal.FreeHGlobal(filePtr);
        }
    }

    [DllImport("MAPI32.DLL", CharSet = CharSet.Unicode)]
    private static extern int MAPISendMailW(
        IntPtr session,
        IntPtr uiParam,
        ref MapiMessageW message,
        int flags,
        int reserved);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MapiFileDescW
    {
        public int reserved;
        public int flags;
        public uint position;
        public string pathName;
        public string fileName;
        public IntPtr fileType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MapiMessageW
    {
        public int reserved;
        public string subject;
        public string noteText;
        public string? messageType;
        public string? dateReceived;
        public string? conversationID;
        public int flags;
        public IntPtr originator;
        public int recipCount;
        public IntPtr recips;
        public int fileCount;
        public IntPtr files;
    }
}
