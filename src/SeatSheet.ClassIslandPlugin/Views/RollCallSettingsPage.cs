using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using FluentAvalonia.UI.Controls;
using SeatSheet.ClassIslandPlugin.Presentation;
using SeatSheet.ClassIslandPlugin.Reception;
using SeatSheet.RollCall.Protocol;

namespace SeatSheet.ClassIslandPlugin.Views;

[SettingsPageInfo("seatsheet.widget.rollcall.settings", "SeatSheet 点名")]
[FullWidthPage]
public sealed class RollCallSettingsPage : SettingsPageBase
{
    private CancellationTokenSource? _pendingSave;

    public RollCallSettingsPage(RollCallReceiver receiver, NotificationDisplaySettingsStore settings, ClientConnectionState connection)
    {
        var saveState = SmallText(settings.LoadFailed
            ? "原设置无法读取，已使用默认 5 秒；调整后将重新保存。"
            : "自动保存，重启后继续使用。", 12);
        var durationText = new TextBlock
        {
            Text = $"{settings.DurationSeconds} 秒", MinWidth = 42,
            VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right
        };
        var slider = new Slider
        {
            Name = "NotificationDuration", Minimum = NotificationDisplaySettings.MinimumSeconds,
            Maximum = NotificationDisplaySettings.MaximumSeconds, TickFrequency = 1,
            IsSnapToTickEnabled = true, Value = settings.DurationSeconds, Width = 170,
            VerticalAlignment = VerticalAlignment.Center
        };
        var testState = SmallText("显示虚构的示例同学、班级和座位，不会抽取真实学生。", 13);
        var readiness = new TextBlock { Text = receiver.Ready ? "已就绪" : "未就绪", VerticalAlignment = VerticalAlignment.Center };
        var clientStatus = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        void RefreshStatus()
        {
            readiness.Text = receiver.Ready ? "已就绪" : "未就绪";
            clientStatus.Text = receiver.Ready && connection.Connected ? "已连接" : "未连接";
        }
        RefreshStatus();
        // Refresh only while this page is visible; no background heartbeat polling.
        var statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        statusTimer.Tick += (_, _) => RefreshStatus();
        Loaded += (_, _) => { RefreshStatus(); statusTimer.Start(); };
        var test = new Button
        {
            Name = "TestNotification", Content = "测试提醒", MinWidth = 100,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        async Task<bool> SaveAsync(int seconds)
        {
            try
            {
                await settings.SaveDurationAsync(seconds);
                saveState.Text = $"已保存 · {seconds} 秒";
                return true;
            }
            catch
            {
                saveState.Text = $"保存失败，提醒仍使用 {settings.DurationSeconds} 秒；请检查配置目录权限。";
                return false;
            }
        }

        void CancelPendingSave()
        {
            _pendingSave?.Cancel();
            _pendingSave?.Dispose();
            _pendingSave = null;
        }

        slider.PropertyChanged += async (_, args) =>
        {
            if (args.Property != Slider.ValueProperty) return;
            var seconds = (int)Math.Round(slider.Value);
            durationText.Text = $"{seconds} 秒";
            CancelPendingSave();
            _pendingSave = new CancellationTokenSource();
            var token = _pendingSave.Token;
            saveState.Text = "正在保存…";
            try
            {
                // Avoid one disk write per mouse movement while dragging the slider.
                await Task.Delay(350, token);
                await SaveAsync(seconds);
            }
            catch (OperationCanceledException) { }
        };
        Unloaded += async (_, _) =>
        {
            statusTimer.Stop();
            CancelPendingSave();
            await SaveAsync((int)Math.Round(slider.Value));
        };
        test.Click += async (_, _) =>
        {
            test.IsEnabled = false;
            CancelPendingSave();
            try
            {
                if (!await SaveAsync((int)Math.Round(slider.Value)))
                {
                    testState.Text = "显示时长尚未保存，请调整后重试。";
                    return;
                }
                testState.Text = "正在发送示例提醒…";
                var now = DateTimeOffset.UtcNow;
                var receipt = await receiver.ReceiveAsync(new RollCallMessage
                {
                    MessageId = Guid.NewGuid().ToString("D"), CreatedAtUtc = now, ExpiresAtUtc = now.AddSeconds(30),
                    Student = new() { Name = "示例同学", ClassName = "示例班级", Seat = new() { Row = 2, Column = 3 } }
                });
                readiness.Text = receiver.Ready ? "已就绪" : "未就绪";
                testState.Text = receipt.Status switch
                {
                    "accepted" => $"已交给 ClassIsland，显示约 {settings.DurationSeconds} 秒。若未出现，请检查原生提醒开关。",
                    "busy" => "提醒请求较多，请稍后再试。",
                    "expired" => "示例提醒已过期，请重新测试。",
                    _ => "测试未成功，请检查 ClassIsland 日志和插件配置目录。"
                };
            }
            catch { testState.Text = "测试失败，请检查 ClassIsland 日志。"; }
            finally { test.IsEnabled = true; }
        };

        var durationControls = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12,
            Children = { slider, durationText }
        };
        Content = new StackPanel
        {
            Margin = new Thickness(12), MaxWidth = 1100, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                SmallText("在这里调整 ClassIsland 的显示效果。名单、抽取和点名规则由座位表软件管理。", 14),
                Card("接收服务", "插件已初始化，可接收点名结果；不代表客户端已连接。", 60871, readiness),
                Card("客户端连接状态", "最近 30 秒内收到桌面端握手或有效结果时显示已连接。", 60871, clientStatus),
                Card("通知显示时长", "结果显示的总时长，包含姓名提示和班级、座位正文。", 62305, durationControls),
                new Border { Padding = new Thickness(16, 0, 16, 8), Child = saveState },
                Card("测试提醒", "按当前显示时长预览一条示例通知。", 60857, test),
                new Border { Padding = new Thickness(16, 0, 16, 8), Child = testState },
                new Border
                {
                    Padding = new Thickness(16, 8),
                    Child = SmallText("当前为接收端试用版。随机抽取、权重、缺席排除及显示目标将由座位表软件提供。", 13)
                }
            }
        };
    }

    private static TextBlock SmallText(string text, double size) => new()
    {
        Text = text, FontSize = size, Opacity = 0.72, TextWrapping = TextWrapping.Wrap
    };

    private static SettingsExpander Card(string title, string description, int iconCode, Control? footer) => new()
    {
        Header = title, Description = description,
        IconSource = new FluentIconSource(char.ConvertFromUtf32(iconCode)), Footer = footer,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
