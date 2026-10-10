using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using ClassIsland.Core.Attributes;
using FluentAvalonia.UI.Controls;

namespace SeatSheet.ClassIslandPlugin.Presentation;

public static class SeatSheetNavigationIcon
{
    public const string PageId = "seatsheet.widget.rollcall.settings";
    private static bool _registered;
    private static readonly Lazy<Geometry> IconGeometry = new(LoadGeometry);
    public static PathIconSource Create() => new()
    {
        Data = IconGeometry.Value,
        // NavigationView reserves a larger icon slot than its font glyphs occupy.
        // Keep this drawing at its own size instead of stretching to fill that slot.
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly
    };

    private static Geometry LoadGeometry()
    {
        using var stream = typeof(SeatSheetNavigationIcon).Assembly.GetManifestResourceStream(
            "SeatSheet.ClassIslandPlugin.Assets.seatsheet-outline.svg")
            ?? throw new InvalidOperationException("Missing SeatSheet icon resource.");
        var svg = System.Xml.Linq.XDocument.Load(stream);
        var group = new GeometryGroup { FillRule = FillRule.NonZero };
        foreach (var path in svg.Root!.Elements())
        {
            var shape = StreamGeometry.Parse((string)path.Attribute("d")!);
            if ((string?)path.Attribute("transform") is { } transform)
            {
                // This authored asset contains only translate(x y), not arbitrary SVG.
                var values = transform[10..^1].Split(' ');
                shape.Transform = new TranslateTransform(
                    double.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture));
            }
            group.Children.Add(shape);
        }
        // Keep the authored geometry compact; the actual icon element is capped below.
        group.Transform = new ScaleTransform(0.8, 0.8);
        return group;
    }
    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        // Host themes can give vector icons a larger drawing area than font icons.
        // Limit our actual icon element, not just the source geometry's bounds.
        FAPathIcon.DataProperty.Changed.AddClassHandler<FAPathIcon>((icon, _) =>
        {
            if (!ReferenceEquals(icon.Data, IconGeometry.Value)) return;
            icon.Width = icon.Height = icon.MaxWidth = icon.MaxHeight = 16;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
        });
        // 2.1.0.1 page metadata supports font glyphs only. Replace this plugin's
        // navigation item when the host assigns its tag; leave all other items alone.
        NavigationViewItem.TagProperty.Changed.AddClassHandler<NavigationViewItem>((item, _) =>
        {
            if (item.Tag is SettingsPageInfo info && info.Id == PageId) item.IconSource = Create();
        });
    }
}
