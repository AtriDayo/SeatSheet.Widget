using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SeatSheet.RollCall.Protocol;
namespace SeatSheet.Widget;
public partial class RollCallResultWindow : Window
{
    private readonly Func<Task> _next, _retry;
    private bool external;
    public RollCallResultWindow(Func<Task> next, Func<Task> retry)
    {
        InitializeComponent(); _next = next; _retry = retry;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
    public void SetResult(RollCallMessage message, bool useExternal)
    {
        external = useExternal;
        StudentName.Text = message.Student!.Name;
        StudentDetails.Text = $"{message.Student.ClassName} · 第{message.Student.Seat!.Row}排第{message.Student.Seat.Column}列";
        RetryButton.Visibility = external ? Visibility.Visible : Visibility.Collapsed;
    }
    public void SetStatus(string status) => DeliveryState.Text = status;
    public void SetAvailability(bool busy, bool paused)
    {
        NextButton.IsEnabled = !busy && !paused; RetryButton.IsEnabled = !busy && !paused && external;
        NextButton.ToolTip = RetryButton.ToolTip = paused ? "当前为课间，已禁用点名；上课后自动恢复。" : null;
        System.Windows.Controls.ToolTipService.SetShowOnDisabled(NextButton, true);
        System.Windows.Controls.ToolTipService.SetShowOnDisabled(RetryButton, true);
    }
    private async void NextClick(object sender, RoutedEventArgs e) => await _next();
    private async void RetryClick(object sender, RoutedEventArgs e) => await _retry();
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || e.OriginalSource is not DependencyObject source) return;
        for (var node = source; node != null; node = node is System.Windows.Media.Visual
            ? System.Windows.Media.VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is System.Windows.Controls.Button) return;
        DragMove();
    }
}
