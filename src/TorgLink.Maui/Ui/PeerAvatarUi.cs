using ShortP2P.Auth.Data;

namespace TorgLink.Maui;

/// <summary>Builds MAUI <see cref="ImageSource"/> from peer-profile avatar bytes (size-capped).</summary>
internal static class PeerAvatarUi
{
    public static ImageSource? ToImageSource(byte[]? avatar)
    {
        if (avatar is not { Length: > 0 })
            return null;
        if (avatar.Length > PeerProfileLimits.MaxAvatarDisplayBytes)
            return null;

        var copy = avatar.ToArray();
        return ImageSource.FromStream(() => new MemoryStream(copy));
    }
}
