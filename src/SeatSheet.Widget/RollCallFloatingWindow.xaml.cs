using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace SeatSheet.Widget;

public partial class RollCallFloatingWindow : Window
{
    private readonly Func<Task> draw;
    public RollCallFloatingWindow(Func<Task> draw)
    {
        InitializeComponent(); this.draw = draw;
        SourceInitialized += (_, _) => Native.MakeToolWindow(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Left + Math.Max(0, area.Width - Width) * App.Settings.RollCallLeftRatio;
            Top = area.Top + Math.Max(0, area.Height - Height) * App.Settings.RollCallTopRatio;
        };
    }
    public void SetAvailability(bool busy, bool paused)
    {
        DrawButton.IsEnabled = !busy && !paused;
        if (paused) SetStatus("当前为课间，已禁用点名；上课后自动恢复。");
        else if (busy) SetStatus("正在点名，请稍候。");
        else if (DrawButton.ToolTip is string status && status.Contains("课间")) SetStatus("使用最近加载的名单点名");
    }
    public void SetStatus(string status)
    {
        DrawButton.ToolTip = status;
    }
    private async void DrawClick(object sender, RoutedEventArgs e) => await draw();
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || e.OriginalSource is not DependencyObject node) return;
        for (var current = node; current != null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is Button) return;
        DragMove();
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
        App.Settings.RollCallLeftRatio = (Left - area.Left) / Math.Max(1, area.Width - Width);
        App.Settings.RollCallTopRatio = (Top - area.Top) / Math.Max(1, area.Height - Height);
        try { LocalStore.SaveSettings(); } catch { SetStatus("位置仅在本次使用中保留，未能保存到本机。"); }
    }
}
