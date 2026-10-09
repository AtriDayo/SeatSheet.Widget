using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SeatSheet.Widget;

public partial class LauncherWindow : Window
{
    private readonly PanelWindow panel;
    private Point start;
    private double startTop;
    private bool pressed, dragged;
    private readonly DispatcherTimer positionTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    public LauncherWindow(PanelWindow panel)
    {
        InitializeComponent();
        this.panel = panel;
        SourceInitialized += (_, _) => Native.MakeToolWindow(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) => Position();
        positionTimer.Tick += (_, _) => { if (!pressed) Position(); };
        positionTimer.Start();
    }
    private void Position()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width;
        Top = work.Top + Math.Max(0, work.Height - Height) * App.Settings.ButtonTopRatio;
    }
    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        pressed = true; dragged = false; start = PointToScreen(e.GetPosition(this)); startTop = Top;
        ((UIElement)sender).CaptureMouse();
    }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (!pressed) return;
        var delta = (PointToScreen(e.GetPosition(this)).Y - start.Y) / (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22 ?? 1);
        if (Math.Abs(delta) > 4) dragged = true;
        if (dragged) Top = Math.Clamp(startTop + delta, SystemParameters.WorkArea.Top, Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - Height));
    }
    private void PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (!pressed) return;
        pressed = false; ((UIElement)sender).ReleaseMouseCapture();
        if (dragged)
        {
            App.Settings.ButtonTopRatio = (Top - SystemParameters.WorkArea.Top) / Math.Max(1, SystemParameters.WorkArea.Height - Height);
            try { LocalStore.SaveSettings(); } catch { /* Position remains usable for this session. */ }
        }
        else panel.Toggle();
    }
    private void OpenPanel(object sender, RoutedEventArgs e) => panel.Open();
    private void SettingsClick(object sender, RoutedEventArgs e) => panel.ShowSettings();
    private void ExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
