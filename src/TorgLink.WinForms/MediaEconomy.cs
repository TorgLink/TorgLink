using ShortP2P.Client.ChatMedia;
using ShortP2P.Discovery;

namespace TorgLink.WinForms;

/// <summary>
/// Media quality / traffic modes aligned with MAUI <c>MediaEconomy</c>.
/// Normal and Economy use ShortP2P ceilings (10 MB image, 20 MB document, 60 MB video).
/// UltraEconomy keeps the 50 KB image cap and ShortP2P super-economy file caps.
/// </summary>
internal static class MediaEconomy
{
    /// <summary>Soft image cap in UltraEconomy (same as MAUI, stricter than ShortP2P 100 KB).</summary>
    private const int MaxImageBytes = 51200;

    private const int EconomyVoiceBitrateBps = 12_000;
    private const int UltraEconomyVoiceBitrateBps = 8_000;
    public const int MinVoiceBitrateBps = UltraEconomyVoiceBitrateBps;
    private const int DefaultSpeechBitrateBps = TrafficQualityModeExtensions.NormalVoiceBitrate;

    private static TrafficQualityMode Mode(P2pRoutingSettings settings) => settings.TrafficQuality;

    public static int SpeechBitrate(P2pRoutingSettings settings) => SpeechBitrate(Mode(settings));

    private static int SpeechBitrate(TrafficQualityMode mode) =>
        mode switch
        {
            TrafficQualityMode.UltraEconomy => UltraEconomyVoiceBitrateBps,
            TrafficQualityMode.Economy => EconomyVoiceBitrateBps,
            _ => DefaultSpeechBitrateBps
        };

    public static bool IsUltraEconomy(TrafficQualityMode mode) => ChatMediaOptions.IsSuperEconomy(mode);

    public static int ImageLimit(P2pRoutingSettings settings) =>
        IsUltraEconomy(Mode(settings)) ? MaxImageBytes : ChatMediaOptions.DefaultMaxImageBytes;

    public static int DocumentLimit(P2pRoutingSettings settings) =>
        IsUltraEconomy(Mode(settings))
            ? ChatMediaOptions.SuperEconomyMaxDocumentBytes
            : ChatMediaOptions.DefaultMaxDocumentBytes;

    public static int VideoLimit(P2pRoutingSettings settings) =>
        IsUltraEconomy(Mode(settings))
            ? ChatMediaOptions.SuperEconomyMaxVideoBytes
            : ChatMediaOptions.DefaultMaxVideoBytes;

    public static int VoiceLimit(P2pRoutingSettings settings) =>
        IsUltraEconomy(Mode(settings))
            ? ChatMediaOptions.SuperEconomyMaxVoiceBytes
            : ChatMediaOptions.DefaultMaxVoiceBytes;

    public static int VoiceMaxSeconds(P2pRoutingSettings settings) =>
        IsUltraEconomy(Mode(settings))
            ? ChatMediaOptions.SuperEconomyMaxVoiceSeconds
            : ChatMediaOptions.MaxVoiceSeconds;

    /// <summary>
    /// Pins shared <see cref="ChatMediaOptions"/> to the non-ultra ceilings.
    /// UltraEconomy keeps ShortP2P super-economy constants.
    /// </summary>
    public static void ApplyStandardCeilings(ChatMediaOptions options)
    {
        options.MaxImageBytes = ChatMediaOptions.DefaultMaxImageBytes;
        options.MaxDocumentBytes = ChatMediaOptions.DefaultMaxDocumentBytes;
        options.MaxVideoBytes = ChatMediaOptions.DefaultMaxVideoBytes;
        options.MaxVoiceBytes = ChatMediaOptions.DefaultMaxVoiceBytes;
    }
}
