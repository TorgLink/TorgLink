using ShortP2P.Client.ChatMedia;
using ShortP2P.Discovery;

namespace TorgLink.Web.Api.Services;

/// <summary>
/// Attachment size caps. UltraEconomy keeps the 50 KB image cap and ShortP2P super-economy
/// file caps. Normal and Economy use 10 MB images, 20 MB documents, and 60 MB video.
/// </summary>
internal static class MediaFileLimits
{
    public const int UltraMaxImageBytes = 50 * 1024;

    /// <summary>Multipart/Kestrel ceiling: 60 MB video plus form framing.</summary>
    public const int MaxUploadBytes = 62 * 1024 * 1024;

    public static bool IsUltraEconomy(TrafficQualityMode mode) => ChatMediaOptions.IsSuperEconomy(mode);

    public static int ImageLimit(TrafficQualityMode mode) =>
        IsUltraEconomy(mode) ? UltraMaxImageBytes : ChatMediaOptions.DefaultMaxImageBytes;

    public static int DocumentLimit(TrafficQualityMode mode) =>
        IsUltraEconomy(mode)
            ? ChatMediaOptions.SuperEconomyMaxDocumentBytes
            : ChatMediaOptions.DefaultMaxDocumentBytes;

    public static int VideoLimit(TrafficQualityMode mode) =>
        IsUltraEconomy(mode)
            ? ChatMediaOptions.SuperEconomyMaxVideoBytes
            : ChatMediaOptions.DefaultMaxVideoBytes;

    public static int VoiceLimit(TrafficQualityMode mode) =>
        IsUltraEconomy(mode)
            ? ChatMediaOptions.SuperEconomyMaxVoiceBytes
            : ChatMediaOptions.DefaultMaxVoiceBytes;

    public static int LimitFor(TrafficQualityMode mode, string? mime)
    {
        if (mime != null && mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return ImageLimit(mode);
        if (mime != null && mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return VideoLimit(mode);
        if (mime != null && mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return VoiceLimit(mode);
        return DocumentLimit(mode);
    }

    public static void ApplyStandardCeilings(ChatMediaOptions options)
    {
        options.MaxImageBytes = ChatMediaOptions.DefaultMaxImageBytes;
        options.MaxDocumentBytes = ChatMediaOptions.DefaultMaxDocumentBytes;
        options.MaxVideoBytes = ChatMediaOptions.DefaultMaxVideoBytes;
        options.MaxVoiceBytes = ChatMediaOptions.DefaultMaxVoiceBytes;
    }
}
