using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.ProfileBackup;
using TorgLink.Localization;

namespace TorgLink.WinForms;

/// <summary>
/// TRL-10 / TRL-24: export/import/share of the Logopass profile as a <c>.tlp</c> file (WinForms dialogs).
/// Export and share live in Settings; import on the login form (auto-login after apply).
/// </summary>
internal static class ProfileFileShare
{
    public static async Task ExportProfileAsync(
        IWin32Window owner,
        AuthService auth,
        ProfileBackupService backup,
        ILogger logger)
    {
        var packed = await BuildEncryptedProfileAsync(owner, auth, backup, logger,
            LocalizationUtils.GetStringByKey("profilex.export_title")).ConfigureAwait(true);
        if (packed == null)
            return;

        var (bytes, suggestedName) = packed.Value;
        using var save = new SaveFileDialog
        {
            Title = LocalizationUtils.GetStringByKey("profilex.export_title"),
            Filter = LocalizationUtils.GetStringByKey("profilex.picker_title") + "|*.tlp",
            DefaultExt = ".tlp",
            AddExtension = true,
            FileName = suggestedName
        };
        if (save.ShowDialog(owner) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllBytes(save.FileName, bytes);
            logger.LogInformation("Profile exported to {FileName}", save.FileName);
            MessageBox.Show(owner,
                LocalizationUtils.GetStringByKey("profilex.exported"),
                LocalizationUtils.GetStringByKey("profilex.export_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Write .tlp profile file");
            MessageBox.Show(owner, ex.Message,
                LocalizationUtils.GetStringByKey("profilex.export_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// TRL-24: write <c>.tlp</c> under Documents\TorgLink, warn about the channel, then email / copy path.
    /// </summary>
    public static async Task ShareProfileAsync(
        IWin32Window owner,
        AuthService auth,
        ProfileBackupService backup,
        ILogger logger)
    {
        var packed = await BuildEncryptedProfileAsync(owner, auth, backup, logger,
            LocalizationUtils.GetStringByKey("profilex.share_title")).ConfigureAwait(true);
        if (packed == null)
            return;

        var (bytes, suggestedName) = packed.Value;
        string path;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "TorgLink");
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, suggestedName);
            File.WriteAllBytes(path, bytes);
            logger.LogInformation("Profile prepared for share at {FileName}", path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Write shared .tlp profile file");
            MessageBox.Show(owner, ex.Message,
                LocalizationUtils.GetStringByKey("profilex.share_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MessageBox.Show(owner,
            LocalizationUtils.GetStringByKey("profilex.share_channel_warn"),
            LocalizationUtils.GetStringByKey("profilex.share_title"),
            MessageBoxButtons.OK, MessageBoxIcon.Warning);

        using var form = new ProfileShareForm(path);
        form.ShowDialog(owner);
    }

    /// <summary>Returns true when the profile was applied and the user is signed in.</summary>
    public static async Task<bool> ImportProfileAsync(
        IWin32Window owner,
        AuthService auth,
        ProfileBackupService backup,
        ILogger logger)
    {
        using var open = new OpenFileDialog
        {
            Title = LocalizationUtils.GetStringByKey("profilex.import_title"),
            Filter = LocalizationUtils.GetStringByKey("profilex.picker_title") + "|*.tlp",
            CheckFileExists = true
        };
        if (open.ShowDialog(owner) != DialogResult.OK)
            return false;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(open.FileName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Read .tlp profile file");
            MessageBox.Show(owner, ex.Message,
                LocalizationUtils.GetStringByKey("profilex.import_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var master = PasswordPromptForm.Prompt(owner,
            LocalizationUtils.GetStringByKey("profilex.import_title"),
            LocalizationUtils.GetStringByKey("profilex.master_unlock_body"),
            LocalizationUtils.GetStringByKey("profilex.master_ph"));
        if (master == null)
            return false;

        try
        {
            var (result, user) = await backup.ImportAsync(bytes, master).ConfigureAwait(true);
            if (!result.Ok || user == null)
            {
                logger.LogWarning("Profile import failed: {Error}", result.Error);
                MessageBox.Show(owner, LocalizeError(result.Error),
                    LocalizationUtils.GetStringByKey("profilex.import_title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Auto-login: persist the session exactly like a password sign-in. Server re-login
            // (AccountPassword) happens silently in the bootstrap that follows DialogResult.OK.
            await auth.AdoptRestoredUserAsync(user).ConfigureAwait(true);
            logger.LogInformation(
                "Profile imported: {Nickname} id={Id}, servers={Servers}",
                user.Nickname,
                user.NetworkIdShort,
                result.Preview?.ServerCount ?? 0);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Profile import failed");
            MessageBox.Show(owner, ex.Message,
                LocalizationUtils.GetStringByKey("profilex.import_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private static async Task<(byte[] bytes, string fileName)?> BuildEncryptedProfileAsync(
        IWin32Window owner,
        AuthService auth,
        ProfileBackupService backup,
        ILogger logger,
        string title)
    {
        var u = auth.CurrentUser;
        if (u == null)
            return null;

        // Мастер-пароль — самостоятельный: задаётся сразу и ни с чем не сравнивается.
        var master = PasswordPromptForm.PromptNew(owner,
            title,
            LocalizationUtils.GetStringByKey("profilex.master_body"),
            LocalizationUtils.GetStringByKey("profilex.master_ph"),
            LocalizationUtils.GetStringByKey("profilex.master_confirm_ph"));
        if (master == null)
            return null;

        try
        {
            var (ok, err, fileBytes) = await backup.ExportAsync(u.Id, master).ConfigureAwait(true);
            if (!ok || fileBytes == null)
            {
                logger.LogWarning("Profile export failed: {Error}", err);
                MessageBox.Show(owner, LocalizeError(err), title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            var name = $"torglink_profile_{DateTime.Now:dd.MM.yyyy_HH_mm_ss}.tlp";
            return (fileBytes, name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Profile export failed");
            MessageBox.Show(owner, ex.Message, title,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
    }

    internal static string LocalizeError(string? err) => err switch
    {
        ProfileBackupService.ErrorMasterPasswordRequired => LocalizationUtils.GetStringByKey("pass.empty"),
        ProfileBackupService.ErrorUnsupportedSaltSize =>
            LocalizationUtils.GetStringByKey("profilex.export_bad_account"),
        ProfileBackupService.ErrorBadFileOrPassword => LocalizationUtils.GetStringByKey("profilex.import_bad_file"),
        ProfileBackupService.ErrorUnsupportedPasswordParams => LocalizationUtils.GetStringByKey("profilex.import_bad_file"),
        ProfileBackupService.ErrorNicknameTaken => LocalizationUtils.GetStringByKey("profilex.import_conflict_nick"),
        null => LocalizationUtils.GetStringByKey("profilex.import_bad_file"),
        _ => err
    };
}
