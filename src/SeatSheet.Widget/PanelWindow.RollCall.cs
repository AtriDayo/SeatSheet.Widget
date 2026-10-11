using System;
using System.Threading.Tasks;
using System.Windows;
using SeatSheet.RollCall.Protocol;
namespace SeatSheet.Widget;
public partial class PanelWindow
{
    private RollCallMessage? lastRollCall;
    private RollCallResultWindow? resultWindow;
    private bool drawBusy;
    private async void RollCallClick(object sender, RoutedEventArgs e) => await DrawStudentAsync();
    internal async Task DrawStudentAsync()
    {
        if (drawBusy) return;
        var source = App.Settings.ServerUrl;
        if (cached == null || cached.Source != source) { SetRollCallStatus("抽取失败：尚无可用座位表，请先刷新。", true); return; }
        var output = App.Settings.RollCall.Output;
        RollCallMessage message;
        try { message = App.RollCall.Draw(source, cached.Plan, App.Settings.RollCall); }
        catch (Exception ex) { SetRollCallStatus("抽取失败：" + ex.Message, true); return; }
        // Fix the result before transport; only Draw creates a new message ID.
        lastRollCall = message;
        drawBusy = true; RollCallButton.IsEnabled = false;
        try
        {
            if (output == "classIsland") resultWindow?.Close();
            if (output != "classIsland") ShowRollCallResult(message, output != "widget");
            resultWindow?.SetBusy(true);
            if (output == "widget") SetRollCallStatus("已抽取并在本软件显示。", false);
            else
            {
                SetRollCallStatus("结果已确定，正在发送通知…", false);
                var delivery = await App.Notifications.SendAsync(message);
                if (!delivery.Success) ShowRollCallResult(message, true);
                SetRollCallStatus(delivery.Success ? delivery.Description : delivery.Description + " 已回退到本软件显示。", false);
            }
        }
        finally { drawBusy = false; RollCallButton.IsEnabled = true; resultWindow?.SetBusy(false); }
    }
    private async Task RetryRollCallAsync()
    {
        if (drawBusy || lastRollCall == null) return;
        var message = lastRollCall;
        drawBusy = true; RollCallButton.IsEnabled = false; resultWindow?.SetBusy(true);
        try
        {
            SetRollCallStatus("正在重发原结果…", false);
            var delivery = await App.Notifications.SendAsync(message);
            SetRollCallStatus(delivery.Description, false);
        }
        finally { drawBusy = false; RollCallButton.IsEnabled = true; resultWindow?.SetBusy(false); }
    }
    private void ShowRollCallResult(RollCallMessage message, bool external)
    {
        if (resultWindow == null)
        {
            resultWindow = new RollCallResultWindow(DrawStudentAsync, RetryRollCallAsync);
            resultWindow.Closed += (_, _) => resultWindow = null;
        }
        resultWindow.SetResult(message, external);
        resultWindow.Show(); resultWindow.Activate();
    }
    private void SetRollCallStatus(string text, bool error)
    {
        RollCallStatus.Text = text; RollCallStatus.Visibility = Visibility.Visible;
        if (error) { ConnectionStatus.Text = "点名失败 · 查看详情"; ConnectionStatus.Foreground = Brush("#A6412F"); }
        else RestoreConnectionSummary();
        resultWindow?.SetStatus(text);
        if (error) resultWindow?.Activate();
    }
}
