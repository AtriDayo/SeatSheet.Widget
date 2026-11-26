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
    private double startTop, dragThreshold;
    private bool pressed, dragged;
    private readonly DispatcherTimer positionTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    public LauncherWindow(PanelWindow panel)
    {
        InitializeComponent();
        this.panel = panel;
        SourceInitialized += (_, _) => Native.MakeToolWindow(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) => ApplyAppearance();
        panel.SettingsSaved += (_, _) => ApplyAppearance();
        ApplyAppearance();
        positionTimer.Tick += (_, _) =>
        {
            if (pressed && (!HandleInput.IsMouseCaptured || Mouse.LeftButton != MouseButtonState.Pressed))
                CancelPointer();
            if (!pressed && IsVisible) Position();
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) CancelPointer(); };
        Closed += (_, _) => positionTimer.Stop();
        positionTimer.Start();
    }
    internal void ApplyAppearance()
    {
        HandleVisual.Kind = App.Settings.LauncherStyle;
        Width = HandleVisual.Width; Height = HandleVisual.Height;
        Position();
    }
    private void Position()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width;
        Top = work.Top + Math.Max(0, work.Height - Height) * App.Settings.ButtonTopRatio;
    }
    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        pressed = HandleInput.CaptureMouse();
        dragged = false;
        start = PointToScreen(e.GetPosition(this));
        startTop = Top;
        dragThreshold = e.StylusDevice == null ? SystemParameters.MinimumVerticalDragDistance : 10;
    }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (!pressed) return;
        var delta = (PointToScreen(e.GetPosition(this)).Y - start.Y) / (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22 ?? 1);
        if (Math.Abs(delta) > dragThreshold) dragged = true;
        if (dragged) Top = Math.Clamp(startTop + delta, SystemParameters.WorkArea.Top, Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - Height));
    }
    private void PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (!pressed) return;
        var moved = dragged;
        CancelPointer();
        if (moved)
        {
            App.Settings.ButtonTopRatio = (Top - SystemParameters.WorkArea.Top) / Math.Max(1, SystemParameters.WorkArea.Height - Height);
            try { LocalStore.SaveSettings(); } catch { /* Position remains usable for this session. */ }
        }
        else panel.Toggle();
    }
    private void CaptureLost(object sender, MouseEventArgs e)
    {
        pressed = dragged = false;
    }
    private void CancelPointer()
    {
        pressed = dragged = false;
        if (HandleInput.IsMouseCaptured) HandleInput.ReleaseMouseCapture();
    }
    private void OpenPanel(object sender, RoutedEventArgs e) => panel.Open();
    private void SettingsClick(object sender, RoutedEventArgs e) => panel.ShowSettings();
    private void ExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
