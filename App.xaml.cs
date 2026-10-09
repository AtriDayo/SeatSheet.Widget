using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace SeatSheet.Widget;

public partial class App : Application
{
    private Mutex? mutex;
    private Forms.NotifyIcon? tray;
    private LauncherWindow? launcher;
    private PanelWindow? panel;
    internal static bool IsSmoke { get; private set; }
    internal static string? SmokeFolder { get; private set; }
    internal static bool CompactSmoke { get; private set; }
    internal static bool IsMotionQa { get; private set; }
    internal static WidgetSettings Settings { get; private set; } = new();
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        IsMotionQa = e.Args.Contains("--motion-qa");
        IsSmoke = e.Args.Contains("--smoke") || IsMotionQa;
        CompactSmoke = e.Args.Contains("--compact");
        if (IsSmoke) SmokeFolder = Path.Combine(AppContext.BaseDirectory, "qa");
        mutex = new Mutex(true, IsSmoke ? "SeatSheet.Widget.Smoke" : "SeatSheet.Widget.Desktop", out var first);
        if (!first) { Shutdown(); return; }
        Settings = IsSmoke ? new WidgetSettings() : LocalStore.LoadSettings();
        panel = new PanelWindow();
        launcher = new LauncherWindow(panel);
        MainWindow = launcher;
        if (!IsSmoke)
        {
            tray = new Forms.NotifyIcon { Text = "SeatSheet · 座位表", Icon = System.Drawing.SystemIcons.Application, Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("查看座位表", null, (_, _) => Dispatcher.Invoke(() => panel.Open()));
            menu.Items.Add("显示 / 隐藏按钮", null, (_, _) => Dispatcher.Invoke(() => { if (launcher.IsVisible) launcher.Hide(); else launcher.Show(); }));
            menu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(() => panel.ShowSettings()));
            menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Shutdown));
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => panel.Open());
        }
        launcher.Show();
        if (IsMotionQa) _ = panel.CheckMotion();
        else if (IsSmoke || e.Args.Contains("--open")) panel.Open();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        mutex?.Dispose();
        base.OnExit(e);
    }
}
