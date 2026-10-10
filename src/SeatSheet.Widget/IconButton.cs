using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace SeatSheet.Widget;

public sealed class IconButton : Button
{
    private readonly VectorIcon glyph = new();
    private readonly TextBlock caption = new();
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(IconButton), new PropertyMetadata("seats", Changed));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(IconButton), new PropertyMetadata("", Changed));
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IconButton()
    {
        Style = (Style)Application.Current.FindResource(typeof(Button));
        glyph.SetBinding(VectorIcon.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
        caption.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
        caption.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(FontSize)) { Source = this });
        glyph.VerticalAlignment = caption.VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(glyph); row.Children.Add(caption); Content = row;
        UpdateContent();
    }
    private static void Changed(DependencyObject owner, DependencyPropertyChangedEventArgs _) => ((IconButton)owner).UpdateContent();
    private void UpdateContent()
    {
        if (glyph == null || caption == null) return;
        glyph.Icon = Icon; caption.Text = Label;
        caption.Margin = new Thickness(string.IsNullOrEmpty(Label) ? 0 : FontSize / 2, 0, 0, 0);
        AutomationProperties.SetName(this, string.IsNullOrEmpty(Label) ? Icon switch { "plus" => "放大座位表", "minus" => "缩小座位表", _ => Icon } : Label);
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty && glyph != null)
        {
            glyph.Width = glyph.Height = FontSize * 4 / 3;
            UpdateContent();
        }
    }
}
