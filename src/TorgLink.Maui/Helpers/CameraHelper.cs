using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;
using Img = SixLabors.ImageSharp.Image;

namespace TorgLink.Maui.Helpers;

/// <summary>
/// A camera photo must be sent exactly as it was displayed.
/// The system camera saves the shot with orientation metadata (rotation,
/// selfie-preview mirror), and not every client applies it — the photo can
/// arrive rotated or mirrored. This helper applies the orientation to the
/// pixels themselves, so the photo is stored upright (left to right) and
/// renders the same way on every platform.
/// </summary>
public static class CameraHelper
{
    /// <summary>
    /// Returns the photo with orientation applied: pixels are already rotated
    /// as needed and the orientation tag is removed, so no viewer rotates the
    /// photo a second time. When the orientation is normal or the file cannot
    /// be decoded, the original bytes are returned unchanged.
    /// </summary>
    public static byte[] Normalize(byte[] source)
    {
        try
        {
            using var image = Img.Load(source);
            var profile = image.Metadata.ExifProfile;
            if (profile is null ||
                !profile.TryGetValue(ExifTag.Orientation, out var orientation) ||
                orientation is null ||
                orientation.Value <= 1)
            {
                return source;
            }

            // Apply the orientation to the pixels (AutoOrient rotates and un-mirrors),
            // then drop the tag: the sent photo must render the same way, without metadata.
            image.Mutate(x => x.AutoOrient());
            profile.RemoveValue(ExifTag.Orientation);
            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms, new JpegEncoder { Quality = 92 });

            return ms.ToArray();
        }
        catch
        {
            // Could not decode — return as is; the send-time checks will filter junk out.
            return source;
        }
    }
}
