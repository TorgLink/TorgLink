namespace TorgLink.Maui.Services;

/// <summary>
/// Hover (мышь) / press (палец) подсветка строки списка чатов или контактов.
/// </summary>
internal static class ListRowHighlight
{
    private static readonly BindableProperty WiredProperty =
        BindableProperty.CreateAttached("Wired", typeof(bool), typeof(ListRowHighlight), false);

    private static readonly BindableProperty HoverProperty =
        BindableProperty.CreateAttached("Hover", typeof(bool), typeof(ListRowHighlight), false);

    private static readonly BindableProperty PressedProperty =
        BindableProperty.CreateAttached("Pressed", typeof(bool), typeof(ListRowHighlight), false);

    private static readonly BindableProperty RestColorProperty =
        BindableProperty.CreateAttached(
            "RestColor",
            typeof(Color),
            typeof(ListRowHighlight),
            null,
            propertyChanged: OnRestColorChanged);

    public static void SetRestColor(BindableObject view, Color? color) =>
        view.SetValue(RestColorProperty, color);

    public static void Attach(View row)
    {
        if (row.GetValue(WiredProperty) is true)
            return;

        row.SetValue(WiredProperty, true);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            row.SetValue(HoverProperty, true);
            Refresh(row);
        };
        pointer.PointerExited += (_, _) =>
        {
            row.SetValue(HoverProperty, false);
            row.SetValue(PressedProperty, false);
            Refresh(row);
        };
        pointer.PointerPressed += (_, _) =>
        {
            row.SetValue(PressedProperty, true);
            Refresh(row);
        };
        pointer.PointerReleased += (_, _) =>
        {
            row.SetValue(PressedProperty, false);
            Refresh(row);
        };
        row.GestureRecognizers.Add(pointer);
    }

    private static void Refresh(View row)
    {
        var on = row.GetValue(HoverProperty) is true || row.GetValue(PressedProperty) is true;
        row.BackgroundColor = on ? ResolveHover() : ResolveRest(row);
    }

    private static void OnRestColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not View row)
            return;
        if (row.GetValue(HoverProperty) is true || row.GetValue(PressedProperty) is true)
            return;
        row.BackgroundColor = newValue as Color ?? Colors.Transparent;
    }

    private static Color ResolveRest(View row) =>
        row.GetValue(RestColorProperty) as Color ?? Colors.Transparent;

    private static Color ResolveHover()
    {
        if (Application.Current?.Resources.TryGetValue("ListRowHover", out var value) == true &&
            value is Color color)
            return color;

        return TorgLinkTheme.Current.IsDark
            ? Color.FromArgb("#3A3A3C")
            : Color.FromArgb("#E5E5EA");
    }
}
