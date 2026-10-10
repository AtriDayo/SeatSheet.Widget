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
    private System.Drawing.Icon? trayIcon;
    private LauncherWindow? launcher;
    private PanelWindow? panel;
    internal static bool IsSmoke { get; private set; }
    internal static string? SmokeFolder { get; private set; }
    internal static bool CompactSmoke { get; private set; }
    internal static bool IsMotionQa { get; private set; }
    internal static bool IsExpansionQa { get; private set; }
    internal static WidgetSettings Settings { get; private set; } = new();
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var appearanceQa = e.Args.Contains("--appearance-qa");
        IsExpansionQa = e.Args.Contains("--expand-qa");
        IsMotionQa = e.Args.Contains("--motion-qa") || IsExpansionQa;
        IsSmoke = e.Args.Contains("--smoke") || IsMotionQa || appearanceQa;
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
            using (var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico")).Stream)
            using (var sourceIcon = new System.Drawing.Icon(iconStream))
                trayIcon = new System.Drawing.Icon(sourceIcon, new System.Drawing.Size(32, 32));
            tray = new Forms.NotifyIcon { Text = "SeatSheet · 座位表", Icon = trayIcon, Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("查看座位表", null, (_, _) => Dispatcher.Invoke(() => panel.Open()));
            menu.Items.Add("显示 / 隐藏按钮", null, (_, _) => Dispatcher.Invoke(() => { if (launcher.IsVisible) launcher.Hide(); else launcher.Show(); }));
            menu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(() => panel.ShowSettings()));
            menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Shutdown));
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => panel.Open());
        }
        launcher.Show();
        if (appearanceQa) _ = CheckAppearance();
        else if (IsExpansionQa) _ = panel.CheckExpansion();
        else if (IsMotionQa) _ = panel.CheckMotion();
        else if (e.Args.Contains("--settings")) panel.ShowSettings();
        else if (IsSmoke || e.Args.Contains("--open")) panel.Open();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        trayIcon?.Dispose();
        mutex?.Dispose();
        base.OnExit(e);
    }
}
