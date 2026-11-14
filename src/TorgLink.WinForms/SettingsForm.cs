using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.ProfileBackup;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Crypto;
using ShortP2P.Discovery;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using TorgLink.Localization;
using TorgLink.WinForms.Localization;
using Img = SixLabors.ImageSharp.Image;
using Rectangle = SixLabors.ImageSharp.Rectangle;

namespace TorgLink.WinForms;

/// <summary>
/// Maui Settings subset that works without BLE/camera: profile (avatar/about inline), LAN, routing, economy, keys, about.
/// </summary>
public sealed partial class SettingsForm : AppForm
{
    private const int AvatarDimension = 512;

    private readonly AuthService _auth = null!;
    private readonly ProfileBackupService _backup = null!;
    private readonly ChatRepository _chats = null!;
    private readonly ChatSessionCache _sessions = null!;
    private readonly MessengerServerSyncService _sync = null!;
    private readonly P2pRoutingSettings _live = null!;
    private readonly P2pRoutingSettingsStore _store = null!;
    private readonly ILogger<SettingsForm> _logger = null!;

    private byte[]? _avatarBytes;
    private TabPage? _tabLanguage;
    private LanguageTilesPanel? _languageTiles;
    private Label? _languageWarning;
    private Label? _languageSection;

    public SettingsForm()
    {
        InitializeComponent();
    }

    public SettingsForm(
        AuthService auth,
        ProfileBackupService backup,
        ChatRepository chats,
        ChatSessionCache sessions,
        MessengerServerSyncService sync,
        P2pRoutingSettings live,
        P2pRoutingSettingsStore store,
        ILogger<SettingsForm> logger)
        : this()
    {
        _auth = auth;
        _backup = backup;
        _chats = chats;
        _sessions = sessions;
        _sync = sync;
        _live = live;
        _store = store;
        _logger = logger;

        BuildLanguageTab();

        foreach (var mode in new[]
                 {
                     TrafficQualityMode.Normal, TrafficQualityMode.Economy, TrafficQualityMode.UltraEconomy
                 })
            _economy.Items.Add(new EconomyItem(mode));

        foreach (var p in LinkTechnologyPresetExtensions.AllPresets)
            _link.Items.Add(new LinkItem(p));

        _economy.SelectedIndexChanged += (_, _) => UpdateEconomyHint();

        _aboutMe.MaxLength = PeerProfileLimits.MaxAboutMeChars;

        _loadAvatar.Click += (_, _) => OnLoadAvatar();
        _clearAvatar.Click += (_, _) =>
        {
            _avatarBytes = null;
            ApplyAvatarPreview(null);
        };
        _aboutMe.TextChanged += (_, _) => UpdateAboutCounter();

        _save.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        _keys.Click += (_, _) => CopyKeys();
        _exportProfile.Click += async (_, _) =>
            await ProfileFileShare.ExportProfileAsync(this, _auth, _backup, _logger).ConfigureAwait(true);
        _shareProfile.Click += async (_, _) =>
            await ProfileFileShare.ShareProfileAsync(this, _auth, _backup, _logger).ConfigureAwait(true);
        _about.Click += (_, _) => MessageBox.Show(this,
            LocalizationUtils.GetStringByKey("settings.about_body") +
            "\nTorgLink.WinForms 0.2.0 (.NET Framework 4.7.2)\nWindows 7 SP1+",
            "TorgLink", MessageBoxButtons.OK, MessageBoxIcon.Information);

        // Defer full ApplyLocalizedUi until Load (TabControl handle exists). Setting TabPage.Text
        // before the native handle is ready throws ArgumentOutOfRangeException (index -1).
        Load += async (_, _) =>
        {
            ApplyLocalizedUi();
            await LoadAsync().ConfigureAwait(true);
        };
    }

    private void BuildLanguageTab()
    {
        // Set Text while Parent is null — avoids TabControl.UpdateTab(index: -1).
        _tabLanguage = new TabPage
        {
            Name = "_tabLanguage",
            Text = LocalizationUtils.GetStringByKey("lang.title"),
            UseVisualStyleBackColor = true,
            Padding = new Padding(12)
        };
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            AutoScroll = true,
            Padding = new Padding(24, 16, 24, 16)
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _languageSection = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 12),
            Text = LocalizationUtils.GetStringByKey("lang.choose")
        };
        _languageTiles = new LanguageTilesPanel(LanguageService.Current, OnLanguageTileSelected)
        {
            Margin = new Padding(0, 4, 0, 4)
        };
        _languageWarning = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.None,
            ForeColor = System.Drawing.Color.DarkOrange,
            MaximumSize = new System.Drawing.Size(396, 0),
            Margin = new Padding(0, 12, 0, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        page.Controls.Add(_languageSection, 0, 0);
        page.Controls.Add(_languageTiles, 0, 1);
        page.Controls.Add(_languageWarning, 0, 2);
        _tabLanguage.Controls.Add(page);
        // Insert before the native handle exists is a no-op in the tab list
        // (the page is parented but never shown). Add registers it either way.
        _tabs.TabPages.Add(_tabLanguage);
    }

    /// <summary>
    /// TabPage.Text → UpdateParent → TabControl.UpdateTab requires a valid index in TabPages.
    /// Before the TabControl handle exists (or if the page is not in the collection), IndexOf is -1 and WinForms throws.
    /// </summary>
    private static void SetTabPageText(TabPage? page, string text)
    {
        if (page == null || page.IsDisposed)
            return;
        if (string.Equals(page.Text, text, StringComparison.Ordinal))
            return;

        if (page.Parent is TabControl tabControl)
        {
            if (!tabControl.IsHandleCreated || tabControl.TabPages.IndexOf(page) < 0)
                return;
        }

        page.Text = text;
    }

    private void OnLanguageTileSelected(AppLanguage language)
    {
        if (language == LanguageService.Current)
            return;
        var warn = LanguageService.TranslationWarning(language);
        if (!string.IsNullOrEmpty(warn))
            MessageBox.Show(this, warn, LanguageService.NativeName(language), MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        LanguageService.Set(language);
    }

    private async Task LoadAsync()
    {
        UpdateProfileSummary();
        _udpPort.Text = _auth.CurrentUser?.DataUdpPort.ToString() ?? "—";
        LoadProfileIntoUiBestEffort();

        var s = await _store.LoadAsync().ConfigureAwait(true);
        RoutingSettingsLive.Overlay(_live, s);

        _lan.Checked = _live.EnableUdpTransport;
        _shareRoutes.Checked = _live.AdvertisedPeerCapabilities.HasFlag(PresencePeerCapabilities.PeerSearch);
        _visibleForSearch.Checked = _live.VisibleForSearch;
        _hops.Value = Clamp(_live.MaxSearchHops, 1, 3);
        _attempts.Value = Clamp(_live.SendFailureSearchAttempts, 1, 20);
        _delayMs.Value = Clamp((decimal)_live.SendFailureRetryDelay.TotalMilliseconds, 0, 3_600_000);
        _timeoutMs.Value = Clamp((decimal)_live.SearchWaitTimeout.TotalMilliseconds, 500, 120_000);

        SelectEconomy(_live.TrafficQuality);
        var li = Array.IndexOf(LinkTechnologyPresetExtensions.AllPresets, _live.LinkTechnology);
        _link.SelectedIndex = li >= 0 ? li : 0;
        UpdateEconomyHint();
    }

    protected override void ApplyLocalizedUi()
    {
        Text = LocalizationUtils.GetStringByKey("settings.title");
        SetTabPageText(_tabLanguage, LocalizationUtils.GetStringByKey("lang.title"));
        if (_languageSection != null)
            _languageSection.Text = LocalizationUtils.GetStringByKey("lang.choose");
        if (_languageTiles != null)
            _languageTiles.SetSelected(LanguageService.Current);
        if (_languageWarning != null)
        {
            var warn = LanguageService.TranslationWarning(LanguageService.Current);
            _languageWarning.Text = warn;
            _languageWarning.Visible = !string.IsNullOrEmpty(warn);
        }

        SetTabPageText(_tabProfile, LocalizationUtils.GetStringByKey("profile.title"));
        SetTabPageText(_tabNetwork, LocalizationUtils.GetStringByKey("tab.network"));
        SetTabPageText(_tabRouting, LocalizationUtils.GetStringByKey("routing.title"));

        _loadAvatar.Text = LocalizationUtils.GetStringByKey("profile.choose_avatar");
        _clearAvatar.Text = LocalizationUtils.GetStringByKey("profile.clear_avatar");
        _aboutLabel.Text = LocalizationUtils.GetStringByKeyWithFormat("profile.about_ph", PeerProfileLimits.MaxAboutMeChars);

        _lblUdpCaption.Text = LocalizationUtils.GetStringByKey("settings.udp");
        _lan.Text = LocalizationUtils.GetStringByKey("settings.lan");
        _shareRoutes.Text = LocalizationUtils.GetStringByKey("routing.share_routes");
        _visibleForSearch.Text = LocalizationUtils.GetStringByKey("settings.visible_for_search");
        _lblEconomy.Text = LocalizationUtils.GetStringByKey("settings.economy");
        _lblNoBle.Text = LocalizationUtils.GetStringByKey("settings.bluetooth") + " — n/a";

        _lblHops.Text = LocalizationUtils.GetStringByKey("routing.max_depth");
        _lblAttempts.Text = LocalizationUtils.GetStringByKey("routing.attempts");
        _lblDelay.Text = LocalizationUtils.GetStringByKey("routing.delay");
        _lblTimeout.Text = LocalizationUtils.GetStringByKey("routing.timeout");
        _lblLink.Text = LocalizationUtils.GetStringByKey("routing.speed");

        _save.Text = LocalizationUtils.GetStringByKey("save");
        _keys.Text = LocalizationUtils.GetStringByKey("settings.export_keys");
        _exportProfile.Text = LocalizationUtils.GetStringByKey("settings.export_profile");
        _shareProfile.Text = LocalizationUtils.GetStringByKey("settings.share_profile");
        _about.Text = LocalizationUtils.GetStringByKey("settings.about");
        _close.Text = LocalizationUtils.GetStringByKey("close");

        RefreshEconomyLabels();
        UpdateEconomyHint();
        UpdateProfileSummary();
        UpdateAboutCounter();
    }

    private void RefreshEconomyLabels()
    {
        var selected = SelectedEconomy();
        _economy.Items.Clear();
        foreach (var mode in new[]
                 {
                     TrafficQualityMode.Normal, TrafficQualityMode.Economy, TrafficQualityMode.UltraEconomy
                 })
            _economy.Items.Add(new EconomyItem(mode));
        SelectEconomy(selected);
    }

    private void UpdateProfileSummary()
    {
        var u = _auth.CurrentUser;
        var about = u != null && !string.IsNullOrWhiteSpace(u.AboutMe)
            ? $" · {TrimAbout(u.AboutMe, 60)}"
            : "";
        _profile.Text = u == null
            ? LocalizationUtils.GetStringByKey("login.failed")
            : $"{u.Nickname}  ·  {u.NetworkIdShort}{about}";
    }

    private void LoadProfileIntoUiBestEffort()
    {
        try
        {
            var user = _auth.CurrentUser;
            _aboutMe.Text = user?.AboutMe ?? "";
            _avatarBytes = user?.Avatar;
            ApplyAvatarPreview(_avatarBytes);
            UpdateAboutCounter();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load own profile into UI (best-effort); continuing with empty fields");
            _aboutMe.Text = "";
            _avatarBytes = null;
            ApplyAvatarPreview(null);
            UpdateAboutCounter();
        }
    }

    private void UpdateAboutCounter()
    {
        _aboutCounter.Text = $"{_aboutMe.Text.Length} / {PeerProfileLimits.MaxAboutMeChars}";
    }

    private void OnLoadAvatar()
    {
        using var dlg = new OpenFileDialog
        {
            Title = LocalizationUtils.GetStringByKey("profile.choose_avatar"),
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All files|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            var raw = File.ReadAllBytes(dlg.FileName);
            if (!TryCropTo512(raw, out var cropped, out var err))
            {
                MessageBox.Show(this, err ?? LocalizationUtils.GetStringByKey("profile.avatar_failed"),
                    LocalizationUtils.GetStringByKey("profile.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            byte[]? prepared = cropped;
            if (cropped!.Length > PeerProfileLimits.MaxAvatarBytes)
            {
                var sizeKb = Math.Max(1, (cropped.Length + 1023) / 1024);
                var limitKb = PeerProfileLimits.MaxAvatarBytes / 1024;
                var answer = MessageBox.Show(this,
                    LocalizationUtils.GetStringByKeyWithFormat("profile.compress_body", sizeKb, limitKb),
                    LocalizationUtils.GetStringByKey("profile.compress_title"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    MessageBox.Show(this,
                        LocalizationUtils.GetStringByKeyWithFormat("profile.avatar_too_large", limitKb),
                        LocalizationUtils.GetStringByKey("profile.title"), MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (!TryCompressToLimit(cropped, out prepared, out err))
                {
                    MessageBox.Show(this, err ?? LocalizationUtils.GetStringByKey("profile.avatar_failed"),
                        LocalizationUtils.GetStringByKey("profile.title"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _avatarBytes = prepared;
            ApplyAvatarPreview(prepared);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load avatar file (best-effort)");
            MessageBox.Show(this, LocalizationUtils.GetStringByKey("profile.avatar_failed"),
                LocalizationUtils.GetStringByKey("profile.title"), MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static bool TryCropTo512(ReadOnlySpan<byte> source,
        [NotNullWhen(true)] out byte[]? output,
        [NotNullWhen(false)] out string? error)
    {
        output = null;
        error = null;
        try
        {
            using var loaded = Img.Load(source);
            var side = Math.Min(loaded.Width, loaded.Height);
            if (side < 1)
            {
                error = "Некорректное изображение.";
                return false;
            }

            var cropX = (loaded.Width - side) / 2;
            var cropY = (loaded.Height - side) / 2;
            loaded.Mutate(x =>
            {
                x.Crop(new Rectangle(cropX, cropY, side, side));
                x.Resize(AvatarDimension, AvatarDimension);
            });

            using var ms = new MemoryStream();
            loaded.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
            output = ms.ToArray();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryCompressToLimit(ReadOnlySpan<byte> source,
        [NotNullWhen(true)] out byte[]? output,
        [NotNullWhen(false)] out string? error)
    {
        output = null;
        error = null;
        try
        {
            using var loaded = Img.Load(source);
            for (var attempt = 0; attempt < 45; attempt++)
            {
                var q = Math.Max(22, Math.Min(88, 88 - attempt));
                var scale = attempt < 12 ? 1.0f : (float)Math.Pow(0.92, attempt - 11);
                using var work = loaded.Clone(x =>
                {
                    if (scale < 0.999f)
                    {
                        var w = Math.Max(48, (int)(AvatarDimension * scale));
                        var h = Math.Max(48, (int)(AvatarDimension * scale));
                        x.Resize(w, h);
                    }
                });
                using var ms = new MemoryStream();
                work.SaveAsJpeg(ms, new JpegEncoder { Quality = q });
                var bytes = ms.ToArray();
                if (bytes.Length <= PeerProfileLimits.MaxAvatarBytes)
                {
                    output = bytes;
                    return true;
                }
            }

            error = $"Не удалось уложить аватар в {PeerProfileLimits.MaxAvatarBytes / 1024} КБ.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void ApplyAvatarPreview(byte[]? bytes)
    {
        var previous = _avatarPreview.Image;
        _avatarPreview.Image = null;
        previous?.Dispose();
        if (bytes == null || bytes.Length == 0)
            return;
        try
        {
            using var ms = new MemoryStream(bytes, false);
            using var img = System.Drawing.Image.FromStream(ms);
            _avatarPreview.Image = new Bitmap(img);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to preview avatar (best-effort)");
        }
    }

    private void SelectEconomy(TrafficQualityMode mode)
    {
        for (var i = 0; i < _economy.Items.Count; i++)
        {
            if (_economy.Items[i] is EconomyItem item && item.Mode == mode)
            {
                _economy.SelectedIndex = i;
                return;
            }
        }

        _economy.SelectedIndex = 0;
    }

    private void UpdateEconomyHint()
    {
        var mode = SelectedEconomy();
        var (w, h) = mode.GetVideoResolution();
        _economyHint.Text =
            $"голос {VoiceKbps(mode)} kbit/s  ·  видео {w}×{h}, {mode.GetCameraVideoBitrate() / 1000} kbit/s";
    }

    private static int VoiceKbps(TrafficQualityMode mode) =>
        mode switch
        {
            TrafficQualityMode.UltraEconomy => 8,
            TrafficQualityMode.Economy => 12,
            _ => 24
        };

    private TrafficQualityMode SelectedEconomy() =>
        _economy.SelectedItem is EconomyItem e ? e.Mode : TrafficQualityMode.Normal;

    private async Task SaveAsync()
    {
        try
        {
            if (_auth.CurrentUser != null)
            {
                var (ok, error) = await _auth.UpdateProfileAsync(_aboutMe.Text, _avatarBytes)
                    .ConfigureAwait(true);
                if (!ok)
                {
                    MessageBox.Show(this, error ?? LocalizationUtils.GetStringByKey("profile.save_failed"),
                        LocalizationUtils.GetStringByKey("profile.title"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _ = BroadcastLocalUserInfoToContactsAsync();
            }

            var s = await _store.LoadAsync().ConfigureAwait(true);
            s.EnableUdpTransport = _lan.Checked;
            s.EnableBluetoothTransport = false;
            s.TrafficQuality = SelectedEconomy();
            s.MaxSearchHops = (int)_hops.Value;
            s.SendFailureSearchAttempts = (int)_attempts.Value;
            s.SendFailureRetryDelay = TimeSpan.FromMilliseconds((double)_delayMs.Value);
            s.SearchWaitTimeout = TimeSpan.FromMilliseconds((double)_timeoutMs.Value);
            if (_link.SelectedItem is LinkItem li)
                s.LinkTechnology = li.Preset;

            var cap = (s.AdvertisedPeerCapabilities & ~PresencePeerCapabilities.PeerSearch) |
                      PresencePeerCapabilities.Chat;
            if (_shareRoutes.Checked)
                cap |= PresencePeerCapabilities.PeerSearch;
            s.AdvertisedPeerCapabilities = cap;
            s.VisibleForSearch = _visibleForSearch.Checked;

            await _store.SaveAsync(s).ConfigureAwait(false);
            RoutingSettingsLive.Overlay(_live, s);
            _sync.Manager.VisibleForSearch = s.VisibleForSearch;
            _ = _sync.ChangeVisibilityOnAllServersAsync(s.VisibleForSearch);
            _logger.LogInformation("Settings saved: udp={Udp} quality={Quality} hops={Hops} visible={Visible}",
                s.EnableUdpTransport, s.TrafficQuality, s.MaxSearchHops, s.VisibleForSearch);
            UpdateProfileSummary();
            MessageBox.Show(this, LocalizationUtils.GetStringByKey("profile.saved"),
                LocalizationUtils.GetStringByKey("settings.title"), MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Save settings");
            MessageBox.Show(this, ex.Message, LocalizationUtils.GetStringByKey("settings.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>TRL-7: push AboutMe + Avatar to every chat contact via messenger-server path (best-effort).</summary>
    private async Task BroadcastLocalUserInfoToContactsAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;

        try
        {
            var chats = await _chats.ListChatsAsync(user.Id).ConfigureAwait(false);
            foreach (var chat in chats)
            {
                try
                {
                    if (await _chats.IsPeerBlockedAsync(user.Id, chat.PeerNetworkIdShort).ConfigureAwait(false))
                        continue;

                    var session = _sessions.GetSession(
                        chat.Id,
                        () => new ChatP2PSession(chat, user, _chats, _sync, null, _logger),
                        s => s.ApplyChatRow(chat));
                    await session.SendLocalUserInfoAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "UserInfo broadcast to chat {ChatId} failed (best-effort)", chat.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UserInfo broadcast failed (best-effort)");
        }
    }

    private void CopyKeys()
    {
        var u = _auth.CurrentUser;
        if (u == null)
            return;
        try
        {
            var pub = RsaKeySerializer.SerializePublic(_auth.GetCurrentPublicKey());
            var text = $"Network id: {u.NetworkIdShort}\nPublic key JSON:\n{pub}";
            Clipboard.SetText(text);
            MessageBox.Show(this, LocalizationUtils.GetStringByKey("copied.keys"),
                LocalizationUtils.GetStringByKey("settings.title"), MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Copy keys");
            MessageBox.Show(this, ex.Message, LocalizationUtils.GetStringByKey("settings.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        value < min ? min : value > max ? max : value;

    private static decimal Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;

    private static string TrimAbout(string text, int max)
    {
        var t = text.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }

    private sealed record EconomyItem(TrafficQualityMode Mode)
    {
        public override string ToString() => Mode switch
        {
            TrafficQualityMode.UltraEconomy => LocalizationUtils.GetStringByKey("economy.mode.ultra"),
            TrafficQualityMode.Economy => LocalizationUtils.GetStringByKey("economy.mode.economy"),
            _ => LocalizationUtils.GetStringByKey("economy.mode.normal")
        };
    }

    private sealed record LinkItem(LinkTechnologyPreset Preset)
    {
        public override string ToString() => Preset.GetDisplayLabel();
    }
}
