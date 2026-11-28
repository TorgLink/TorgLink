using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.ProfileBackup;
using TorgLink.Maui.Services;
using TorgLink.Localization;

namespace TorgLink.Maui;

/// <summary>
/// TRL-10 / TRL-24: export/import/share of the Logopass profile as a <c>.tlp</c> file.
/// Export and share live in Settings; import on the login form (auto-login after apply).
/// </summary>
internal static class ProfileFileShare
{
    public static async Task ExportProfileAsync(
        Page host,
        AuthService auth,
        ProfileBackupService backup,
        ILogger logger)
    {
        var packed = await BuildEncryptedProfileAsync(host, auth, backup, logger,
            LocalizationUtils.GetStringByKey("profilex.export_title")).ConfigureAwait(true);
        if (packed == null)
            return;

        var (bytes, name) = packed.Value;
        var temp = Path.Combine(FileSystem.CacheDirectory, name);
        await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(true);
        var saved = await MediaFileSaver.SaveAsync(temp, name).ConfigureAwait(true);
        if (saved)
        {
            AppLog.Ui.LogInformation("Profile exported to {FileName}", name);
            await host.DisplayAlert(LocalizationUtils.GetStringByKey("profilex.export_title"), LocalizationUtils.GetStringByKey("profilex.exported"), LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
        }
    }

    /// <summary>TRL-24: native share sheet after master-password export and a channel warning.</summary>
    public static async Task ShareProfileAsync(
        Page host,
        AuthService auth,
        ProfileBackupService backup,
        ILogger logger)
    {
        var packed = await BuildEncryptedProfileAsync(host, auth, backup, logger,
            LocalizationUtils.GetStringByKey("profilex.share_title")).ConfigureAwait(true);
        if (packed == null)
            return;

        var (bytes, name) = packed.Value;
        var temp = Path.Combine(FileSystem.CacheDirectory, name);
        await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(true);

        await host.DisplayAlert(
                LocalizationUtils.GetStringByKey("profilex.share_title"),
                LocalizationUtils.GetStringByKey("profilex.share_channel_warn"),
                LocalizationUtils.GetStringByKey("ok"))
            .ConfigureAwait(true);

        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = LocalizationUtils.GetStringByKey("profilex.share_title"),
                File = new ShareFile(temp)
            }).ConfigureAwait(true);
            AppLog.Ui.LogInformation("Profile share sheet opened for {FileName}", name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Share profile failed");
            await host.DisplayAlert(
                    LocalizationUtils.GetStringByKey("profilex.share_title"),
                    LocalizationUtils.GetStringByKeyWithFormat("profilex.share_fail", ex.Message),
                    LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
        }
    }

    private static async Task<(byte[] bytes, string fileName)?> BuildEncryptedProfileAsync(
        Page host,
        AuthService auth,
        ProfileBackupService backup,
        ILogger logger,
        string title)
    {
        var u = auth.CurrentUser;
        if (u == null)
            return null;

        // The master password is standalone: set right away, not checked against anything.
        var master = await PasswordPromptPage.ShowNewAsync(host,
            title,
            LocalizationUtils.GetStringByKey("profilex.master_body"),
            LocalizationUtils.GetStringByKey("profilex.master_ph"),
            LocalizationUtils.GetStringByKey("profilex.master_confirm_ph")).ConfigureAwait(true);
        if (master == null)
            return null;

        try
        {
            var (ok, err, fileBytes) = await backup.ExportAsync(u.Id, master).ConfigureAwait(true);
            if (!ok || fileBytes == null)
            {
                logger.LogWarning("Profile export failed: {Error}", err);
                await host.DisplayAlert(title, LocalizeError(err), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return null;
            }

            var name = $"torglink_profile_{DateTime.Now:dd.MM.yyyy_HH_mm_ss}.tlp";
            return (fileBytes, name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Profile export failed");
            await host.DisplayAlert(title, ex.Message, LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return null;
        }
    }

    /// <summary>Returns true when the profile was applied and the user is signed in.</summary>
    public static async Task<bool> ImportProfileAsync(
        Page host,
        AuthService auth,
        ProfileBackupService backup,
        ILogger logger)
    {
        FileResult? picked;
        try
        {
            picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = LocalizationUtils.GetStringByKey("profilex.picker_title"),
                FileTypes = TlpFileTypes
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pick .tlp profile file");
            await host.DisplayAlert(LocalizationUtils.GetStringByKey("profilex.import_title"), ex.Message, LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return false;
        }

        if (picked == null)
            return false;

        byte[] bytes;
        try
        {
            await using var stream = await picked.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            bytes = ms.ToArray();
            AppLog.BinaryLoaded("tlp-profile", picked.FileName, bytes.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Read .tlp profile file");
            await host.DisplayAlert(LocalizationUtils.GetStringByKey("profilex.import_title"), ex.Message, LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return false;
        }

        var master = await PasswordPromptPage.ShowAsync(host,
            LocalizationUtils.GetStringByKey("profilex.import_title"),
            LocalizationUtils.GetStringByKey("profilex.master_unlock_body"),
            LocalizationUtils.GetStringByKey("profilex.master_ph")).ConfigureAwait(true);
        if (master == null)
            return false;

        try
        {
            var (result, user) = await backup.ImportAsync(bytes, master).ConfigureAwait(true);
            if (!result.Ok || user == null)
            {
                logger.LogWarning("Profile import failed: {Error}", result.Error);
                await host.DisplayAlert(LocalizationUtils.GetStringByKey("profilex.import_title"), LocalizeError(result.Error), LocalizationUtils.GetStringByKey("ok"))
                    .ConfigureAwait(true);
                return false;
            }

            // Auto-login: persist the session exactly like a password sign-in. Server re-login
            // (AccountPassword) happens silently once the shell starts connectivity.
            await auth.AdoptRestoredUserAsync(user).ConfigureAwait(true);
            AppLog.Ui.LogInformation(
                "Profile imported: {Nickname} id={Id}, servers={Servers}",
                user.Nickname,
                user.NetworkIdShort,
                result.Preview?.ServerCount ?? 0);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Profile import failed");
            await host.DisplayAlert(LocalizationUtils.GetStringByKey("profilex.import_title"), ex.Message, LocalizationUtils.GetStringByKey("ok"))
                .ConfigureAwait(true);
            return false;
        }
    }

    private static readonly FilePickerFileType TlpFileTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = [".tlp"],
            [DevicePlatform.Android] = ["*/*"]
        });

    private static string LocalizeError(string? err) => err switch
    {
        ProfileBackupService.ErrorMasterPasswordRequired => LocalizationUtils.GetStringByKey("pass.empty"),
        ProfileBackupService.ErrorUnsupportedSaltSize => LocalizationUtils.GetStringByKey("profilex.export_bad_account"),
        ProfileBackupService.ErrorBadFileOrPassword => LocalizationUtils.GetStringByKey("profilex.import_bad_file"),
        ProfileBackupService.ErrorUnsupportedPasswordParams => LocalizationUtils.GetStringByKey("profilex.import_bad_file"),
        ProfileBackupService.ErrorNicknameTaken => LocalizationUtils.GetStringByKey("profilex.import_conflict_nick"),
        null => LocalizationUtils.GetStringByKey("profilex.import_bad_file"),
        _ => err
    };
}

