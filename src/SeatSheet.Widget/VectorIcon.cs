using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml.Linq;

namespace SeatSheet.Widget;

// These project-owned SVG symbols use only path geometry. Draw them natively in WPF.
public sealed class VectorIcon : Viewbox
{
    private static readonly Lazy<Dictionary<string, Geometry[]>> Symbols = new(() =>
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Icons/ui.svg")).Stream;
        var document = XDocument.Load(stream);
        XNamespace svg = "http://www.w3.org/2000/svg";
        return document.Descendants(svg + "symbol").ToDictionary(s => (string)s.Attribute("id")!,
            s => s.Elements(svg + "path").Select(p => { var g = Geometry.Parse((string)p.Attribute("d")!); g.Freeze(); return g; }).ToArray());
    });
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(VectorIcon), new PropertyMetadata("seats", Changed));
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(VectorIcon), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits, Changed));
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public VectorIcon() { Width = Height = 16; Stretch = Stretch.Uniform; IsHitTestVisible = false; Draw(); }
    private static void Changed(DependencyObject owner, DependencyPropertyChangedEventArgs _) => ((VectorIcon)owner).Draw();
    private void Draw()
    {
        var canvas = new Canvas { Width = 24, Height = 24 };
        if (Symbols.Value.TryGetValue(Icon, out var paths))
            foreach (var geometry in paths)
                canvas.Children.Add(new System.Windows.Shapes.Path { Data = geometry, Stroke = Foreground, StrokeThickness = 1.8,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round });
        Child = canvas;
    }
}
