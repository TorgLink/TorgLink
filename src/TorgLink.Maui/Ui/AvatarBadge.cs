using Microsoft.Maui.Controls.Shapes;

namespace TorgLink.Maui;

/// <summary>Applies avatar bytes or letter initials into a circular badge (Border + Label + optional Image).</summary>
internal static class AvatarBadge
{
    public static void Apply(Border fill, Label initials, Image? image, string displayName, string colorKey,
        byte[]? avatar)
    {
        fill.BackgroundColor = TorgLinkTheme.AvatarColor(colorKey);
        initials.Text = TorgLinkTheme.Initials(displayName);
        if (image == null)
        {
            initials.IsVisible = true;
            return;
        }

        if (avatar is { Length: > 0 })
        {
            if (avatar.Length > ShortP2P.Auth.Data.PeerProfileLimits.MaxAvatarDisplayBytes)
            {
                // Oversized remote/local blob: show initials, do not bind huge payload into UI.
                image.Source = null;
                image.IsVisible = false;
                initials.IsVisible = true;
                return;
            }

            try
            {
                var copy = avatar.ToArray();
                var size = image.WidthRequest > 0 ? image.WidthRequest : 40;
                var r = size / 2.0;
                image.Clip ??= new EllipseGeometry(new Point(r, r), r, r);
                image.Source = ImageSource.FromStream(() => new MemoryStream(copy));
                image.IsVisible = true;
                initials.IsVisible = false;
                return;
            }
            catch
            {
                // fall through to initials
            }
        }

        image.Source = null;
        image.IsVisible = false;
        initials.IsVisible = true;
    }
}
