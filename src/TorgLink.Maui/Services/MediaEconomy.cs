using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using TorgLink.Localization;
using TorgLink.LoRa;

namespace TorgLink.Maui.Services;

/// <summary>
/// Media quality / traffic modes aligned with ShortP2P <see cref="TrafficQualityMode"/>.
/// Selection: Settings → traffic quality (persisted in P2pRoutingSettings.TrafficQuality).
/// Normal and Economy use <see cref="ChatMediaOptions"/> ceilings (10 MB image, 20 MB document, 60 MB video).
/// UltraEconomy keeps the TorgLink 50 KB image cap and ShortP2P super-economy file caps.
/// </summary>
internal static class MediaEconomy
{
    /// <summary>Soft image cap in UltraEconomy (stricter than ShortP2P 100 KB).</summary>
    private const int MaxImageBytes = 50 * 1024;

    /// <summary>TorgLink voice rates (override ShortP2P 6 / 4 kbit/s).</summary>
    private const int EconomyVoiceBitrateBps = 12_000;

    private const int UltraEconomyVoiceBitrateBps = 8_000;

    public const int MinVoiceBitrateBps = UltraEconomyVoiceBitrateBps;

    public const int DefaultSpeechBitrateBps = TrafficQualityModeExtensions.NormalVoiceBitrate;

    public static TrafficQualityMode Mode(UserP2pRuntime p2p) => p2p.Settings.TrafficQuality;

    public static bool UsesReducedMedia(UserP2pRuntime p2p) =>
        Mode(p2p) is TrafficQualityMode.Economy or TrafficQualityMode.UltraEconomy;

    public static int BinarySendConcurrency(UserP2pRuntime p2p) =>
        BinarySendScheduler.MaxConcurrency(Mode(p2p));

    public static void Apply(UserP2pRuntime p2p, TrafficQualityMode mode)
    {
        p2p.Settings.TrafficQuality = mode;
    }

    public static int SpeechBitrate(UserP2pRuntime p2p) => SpeechBitrate(Mode(p2p));

    public static int SpeechBitrate(TrafficQualityMode mode) =>
        mode switch
        {
            TrafficQualityMode.UltraEconomy => UltraEconomyVoiceBitrateBps,
            TrafficQualityMode.Economy => EconomyVoiceBitrateBps,
            _ => DefaultSpeechBitrateBps
        };

    private static (int Width, int Height) VideoResolution(UserP2pRuntime p2p) =>
        Mode(p2p).GetVideoResolution();

    public static string VideoResolutionLabel(UserP2pRuntime p2p)
    {
        var (w, h) = VideoResolution(p2p);
        return $"{w}×{h}";
    }

    public static int CameraVideoBitrate(UserP2pRuntime p2p) => Mode(p2p).GetCameraVideoBitrate();

    private static bool IsUltraEconomy(TrafficQualityMode mode) => ChatMediaOptions.IsSuperEconomy(mode);

    public static int ImageLimit(UserP2pRuntime p2p) =>
        CapForLoRa(p2p, IsUltraEconomy(Mode(p2p)) ? MaxImageBytes : ChatMediaOptions.DefaultMaxImageBytes);

    public static int DocumentLimit(UserP2pRuntime p2p) =>
        CapForLoRa(p2p, IsUltraEconomy(Mode(p2p))
            ? ChatMediaOptions.SuperEconomyMaxDocumentBytes
            : ChatMediaOptions.DefaultMaxDocumentBytes);

    public static int VideoLimit(UserP2pRuntime p2p) =>
        CapForLoRa(p2p, IsUltraEconomy(Mode(p2p))
            ? ChatMediaOptions.SuperEconomyMaxVideoBytes
            : ChatMediaOptions.DefaultMaxVideoBytes);

    public static int VoiceLimit(UserP2pRuntime p2p) =>
        CapForLoRa(p2p, IsUltraEconomy(Mode(p2p))
            ? ChatMediaOptions.SuperEconomyMaxVoiceBytes
            : ChatMediaOptions.DefaultMaxVoiceBytes);

    /// <summary>SMT-1 / LoRa: 20 KB client↔client binary ceiling while Meshtastic transport is up.</summary>
    private static int CapForLoRa(UserP2pRuntime p2p, int limit)
    {
        var host = MauiProgram.Services.GetService<MeshtasticTransportHost>();
        if (host is { IsRunning: true })
            return Math.Min(limit, MeshtasticTransportHost.MaxBinaryBytes);

        return limit;
    }

    /// <summary>
    /// Pins shared <see cref="ChatMediaOptions"/> to the non-ultra ceilings so session validation
    /// accepts Normal/Economy payloads. UltraEconomy keeps ShortP2P super-economy constants.
    /// </summary>
    public static void ApplyStandardCeilings(ChatMediaOptions options)
    {
        options.MaxImageBytes = ChatMediaOptions.DefaultMaxImageBytes;
        options.MaxDocumentBytes = ChatMediaOptions.DefaultMaxDocumentBytes;
        options.MaxVideoBytes = ChatMediaOptions.DefaultMaxVideoBytes;
        options.MaxVoiceBytes = ChatMediaOptions.DefaultMaxVoiceBytes;
    }

    public static string Hint(TrafficQualityMode mode)
    {
        var (w, h) = mode.GetVideoResolution();
        var photoKb = IsUltraEconomy(mode) ? (int?)(MaxImageBytes / 1024) : null;
        return photoKb is null
            ? LocalizationUtils.GetStringByKeyWithFormat("economy.hint_normal", SpeechBitrate(mode) / 1000.0, w, h)
            : LocalizationUtils.GetStringByKeyWithFormat("economy.hint", SpeechBitrate(mode) / 1000.0, photoKb.Value, w, h);
    }

    public static string ModeLabel(TrafficQualityMode mode) =>
        mode switch
        {
            TrafficQualityMode.UltraEconomy => LocalizationUtils.GetStringByKey("economy.mode.ultra"),
            TrafficQualityMode.Economy => LocalizationUtils.GetStringByKey("economy.mode.economy"),
            _ => LocalizationUtils.GetStringByKey("economy.mode.normal")
        };

    public static IReadOnlyList<TrafficQualityMode> AllModes { get; } =
    [
        TrafficQualityMode.Normal,
        TrafficQualityMode.Economy,
        TrafficQualityMode.UltraEconomy
    ];
}

