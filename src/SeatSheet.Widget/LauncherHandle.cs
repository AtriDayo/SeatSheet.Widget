using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SeatSheet.Widget;

// Shared by the actual launcher and the settings previews so their silhouettes match.
public sealed class LauncherHandle : Grid
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(LauncherHandle), new PropertyMetadata(LauncherStyles.Label, (owner, _) => ((LauncherHandle)owner).Draw()));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public LauncherHandle() { IsHitTestVisible = false; Draw(); }
    private void Draw()
    {
        Children.Clear();
        var kind = LauncherStyles.Normalize(Kind);
        Width = kind == LauncherStyles.Label ? 30 : kind == LauncherStyles.Slim ? 12 : 24;
        Height = kind == LauncherStyles.Label ? 72 : kind == LauncherStyles.Slim ? 56 : 48;
        var background = new SolidColorBrush(Color.FromRgb(233, 237, 243));
        var edge = new SolidColorBrush(Color.FromRgb(191, 201, 215));
        var ink = new SolidColorBrush(Color.FromRgb(70, 86, 110));
        if (kind == LauncherStyles.Arrow)
        {
            Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M24,0 L24,48 L21,48 C9,48 1,38 1,24 C1,10 9,0 21,0 Z"), Fill = background, Stroke = edge, StrokeThickness = 1 });
            Children.Add(new VectorIcon { Icon = "left", Foreground = ink, Width = 14, Height = 14, Margin = new Thickness(3, 0, 0, 0) });
            return;
        }
        var border = new Border { Background = background, BorderBrush = edge, BorderThickness = new Thickness(1, 1, 0, 1), CornerRadius = new CornerRadius(kind == LauncherStyles.Label ? 8 : 6, 0, 0, kind == LauncherStyles.Label ? 8 : 6) };
        Children.Add(border);
        if (kind == LauncherStyles.Slim)
        {
            border.Child = new Border { Background = new SolidColorBrush(Color.FromRgb(143, 155, 174)), Width = 2, Height = 22, CornerRadius = new CornerRadius(1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            return;
        }
        var words = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-1, 0, 0, 0) };
        foreach (var word in new[] { "座", "位" })
            words.Children.Add(new TextBlock { Text = word, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = ink, Height = 21, LineHeight = 21,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
        border.Child = words;
    }
}
