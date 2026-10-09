using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SeatSheet.Widget;

public partial class PanelWindow : Window
{
    private CachedPlan? cached;
    private bool loading, expanded, fit = true, closing;
    private SettingsWindow? settingsWindow;
    private double boardWidth, boardHeight;
    private bool motionActive;
    private int motionVersion;
    private readonly DispatcherTimer geometryTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    public PanelWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Native.MakeToolWindow(new WindowInteropHelper(this).Handle);
        Closing += OnClosing;
        geometryTimer.Tick += (_, _) => { if (IsVisible && !motionActive) Position(); };
        geometryTimer.Start();
        cached = App.IsSmoke ? null : LocalStore.ReadCache(App.Settings.ServerUrl);
        if (cached != null)
        {
            Render(cached.Plan);
            Status("显示上次保存的座位表，打开时自动刷新", false);
        }
    }
    public void Toggle() { if (IsVisible && !closing) Collapse(); else Open(); }
    public void Open()
    {
        if (IsVisible && !closing) { _ = Refresh(); return; }
        closing = false;
        Position();
        if (!IsVisible)
        {
            MotionSlide.X = 28;
            MotionScale.ScaleX = MotionScale.ScaleY = 0.985;
            MotionRoot.Opacity = 0;
            Show();
            UpdateLayout();
        }
        AnimateMotion(true);
    }
    private void Position()
    {
        var work = SystemParameters.WorkArea;
        if (App.IsSmoke && App.CompactSmoke) work = new Rect(work.Right - 1366, work.Top, 1366, 720);
        var edgeGap = Math.Clamp(Math.Min(work.Width, work.Height) * 0.02, 12, 24);
        var availableWidth = Math.Max(1, work.Width - edgeGap * 2);
        MinWidth = Math.Min(480, availableWidth);
        Width = expanded ? availableWidth : Math.Clamp(App.Settings.PanelWidth, MinWidth, Math.Max(MinWidth, availableWidth * 0.72));
        Height = expanded ? Math.Max(1, work.Height - edgeGap * 2) : Math.Min(720, work.Height * 0.80);
        Top = work.Top + (work.Height - Height) / 2;
        // Keep a gap for the launcher so its second click can always collapse the panel.
        Left = work.Right - Width - Math.Max(edgeGap, expanded ? edgeGap : 36);
        PanelSurface.CornerRadius = new CornerRadius(Math.Clamp(Math.Min(work.Width, work.Height) * 0.018, 12, 24));
        PanelShadow.CornerRadius = PanelSurface.CornerRadius;
    }
    private void AnimateMotion(bool opening)
    {
        var version = ++motionVersion;
        motionActive = true;
        MotionRoot.IsHitTestVisible = false;
        // Cache the stable card only during motion, then restore sharp text rendering.
        MotionRoot.CacheMode = new BitmapCache();
        var duration = TimeSpan.FromMilliseconds(opening ? 300 : 170);
        var spline = opening ? new KeySpline(0.12, 0.85, 0.20, 1.0) : new KeySpline(0.40, 0.0, 0.85, 0.45);
        var x = MotionSlide.X;
        var scale = MotionScale.ScaleX;
        var opacity = MotionRoot.Opacity;
        // Capture current animated values before replacing clocks for continuous reversal.
        MotionSlide.BeginAnimation(TranslateTransform.XProperty, null);
        MotionScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        MotionScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        MotionRoot.BeginAnimation(OpacityProperty, null);
        var targetX = opening ? 0 : 24;
        var targetScale = opening ? 1 : 0.985;
        var targetOpacity = opening ? 1 : 0;
        MotionSlide.X = x;
        MotionScale.ScaleX = MotionScale.ScaleY = scale;
        MotionRoot.Opacity = opacity;
        var slide = MotionAnimation(x, targetX, duration, spline);
        slide.Completed += async (_, _) =>
        {
            if (version != motionVersion) return;
            MotionSlide.BeginAnimation(TranslateTransform.XProperty, null);
            MotionScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            MotionScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            MotionRoot.BeginAnimation(OpacityProperty, null);
            MotionSlide.X = targetX;
            MotionScale.ScaleX = MotionScale.ScaleY = targetScale;
            MotionRoot.Opacity = targetOpacity;
            motionActive = false;
            MotionRoot.CacheMode = null;
            MotionRoot.IsHitTestVisible = opening;
            if (!opening) { Hide(); closing = false; }
            else
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                if (version == motionVersion && !closing && !App.IsMotionQa) await Refresh();
            }
        };
        MotionSlide.BeginAnimation(TranslateTransform.XProperty, slide);
        MotionScale.BeginAnimation(ScaleTransform.ScaleXProperty, MotionAnimation(scale, targetScale, duration, spline));
        MotionScale.BeginAnimation(ScaleTransform.ScaleYProperty, MotionAnimation(scale, targetScale, duration, spline));
        MotionRoot.BeginAnimation(OpacityProperty, MotionAnimation(opacity, targetOpacity, duration, spline));
    }
    private static DoubleAnimationUsingKeyFrames MotionAnimation(double from, double to, TimeSpan duration, KeySpline spline)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = duration, FillBehavior = FillBehavior.HoldEnd };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(duration), spline));
        return animation;
    }
    public void Collapse()
    {
        if (!IsVisible || closing) return;
        closing = true;
        AnimateMotion(false);
    }
    private async Task Refresh()
    {
        if (loading) return;
        var source = App.Settings.ServerUrl;
        loading = true;
        RefreshButton.IsEnabled = false;
        ConnectionStatus.Text = "正在获取最新座位表…";
        try
        {
            var result = await SeatApi.Fetch(source);
            if (source != App.Settings.ServerUrl) return;
            while (motionActive) await Task.Delay(25);
            if (source != App.Settings.ServerUrl) return;
            var changed = cached == null || JsonSerializer.Serialize(cached.Plan, LocalStore.Json) != JsonSerializer.Serialize(result.Plan, LocalStore.Json);
            cached = result;
            if (changed) Render(result.Plan);
            Status("已连接 · 打开面板时自动刷新", true);
            try { LocalStore.SaveCache(result); }
            catch { ConnectionStatus.Text += " · 本次缓存未能保存"; }
        }
        catch (Exception ex)
        {
            if (source != App.Settings.ServerUrl) return;
            var reason = ex switch
            {
                TaskCanceledException => "连接超时，请检查网络或服务地址。",
                HttpRequestException => "暂时无法连接，请确认 SeatSheet 服务已启动。",
                JsonException => "地址没有返回座位表，请在设置中填写 SeatSheet 网站地址。",
                _ => ex.Message
            };
            Status(cached != null ? "离线 · 显示最近一次成功加载的数据。" + reason : reason, false);
            if (cached == null) EmptyTitle.Text = "暂时无法获取座位表";
        }
        finally
        {
            loading = false;
            RefreshButton.IsEnabled = true;
            if (source != App.Settings.ServerUrl) _ = Refresh();
        }
        if (App.IsSmoke) await SmokeCapture();
    }
    private void Status(string message, bool online)
    {
        ConnectionStatus.Text = message;
        ConnectionStatus.Foreground = Brush(online ? "#246849" : "#98621E");
        UpdatedLabel.Text = cached == null ? "" : $"座位更新 {cached.Plan.UpdatedAt.ToLocalTime():MM-dd HH:mm}  ·  获取于 {cached.FetchedAt:MM-dd HH:mm}";
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static TextBlock Label(string text, double size, string color = "#18243B") => new()
    { Text = text, FontSize = size, Foreground = Brush(color), TextAlignment = TextAlignment.Center,
      HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
      TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 132 };
    private void Render(SeatPlan plan)
    {
        EmptyState.Visibility = Visibility.Collapsed;
        PlanTitle.Text = plan.Name;
        PlanStats.Text = $"{plan.Rows} 排 × {plan.Columns} 列   ·   {plan.Seats.Count(s => !string.IsNullOrWhiteSpace(s.Name) || !string.IsNullOrWhiteSpace(s.StudentNo))} 人已安排";
        Board.Children.Clear(); Board.RowDefinitions.Clear(); Board.ColumnDefinitions.Clear();
        var aisles = plan.AisleAfterColumns.Where(c => c >= 0 && c < plan.Columns - 1).Distinct().OrderBy(c => c).ToArray();
        Board.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        for (var column = 0; column < plan.Columns; column++)
        {
            Board.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
            if (aisles.Contains(column)) Board.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        }
        Board.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        for (var row = 0; row < plan.Rows; row++) Board.RowDefinitions.Add(new RowDefinition { Height = new GridLength(98) });
        var seats = plan.Seats.ToDictionary(s => (s.Row, s.Column));
        for (var row = 0; row < plan.Rows; row++)
        for (var column = 0; column < plan.Columns; column++)
        {
            seats.TryGetValue((row, column), out var seat);
            var occupied = !string.IsNullOrWhiteSpace(seat?.Name) || !string.IsNullOrWhiteSpace(seat?.StudentNo);
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(Label(string.IsNullOrWhiteSpace(seat?.Name) ? "空座" : seat.Name, plan.ShowStudentNo ? 23 : 27, occupied ? "#18243B" : "#A0AABD"));
            if (plan.ShowStudentNo) content.Children.Add(new TextBlock
            { Text = string.IsNullOrWhiteSpace(seat?.StudentNo) ? "—" : seat.StudentNo, FontSize = 12, Foreground = Brush("#788397"),
              TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            var card = new Border { Background = Brush(occupied ? "#FFFFFF" : "#EFF2F7"), BorderBrush = Brush("#DFE5EF"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Margin = new Thickness(5), Padding = new Thickness(7), Child = content,
                ToolTip = $"第 {row + 1} 排，第 {column + 1} 列\n{seat?.Name ?? "空座"}" + (plan.ShowStudentNo ? $"\n{seat?.StudentNo}" : "") };
            Grid.SetRow(card, row); Grid.SetColumn(card, 1 + column + aisles.Count(a => a < column));
            Board.Children.Add(card);
        }
        foreach (var aisle in aisles)
        {
            var marker = new Border { BorderBrush = Brush("#D9E1EC"), BorderThickness = new Thickness(1, 0, 1, 0), Margin = new Thickness(5, 10, 5, 10), Child = Label("过\n道", 11, "#8794A8") };
            Grid.SetColumn(marker, aisle + 2 + aisles.Count(a => a < aisle)); Grid.SetRowSpan(marker, plan.Rows);
            Board.Children.Add(marker);
        }
        var doorColumn = plan.DoorSide == "left" ? 0 : Board.ColumnDefinitions.Count - 1;
        var doors = new Grid();
        var front = new Border { Child = Label("前门", 12, "#536581"), Background = Brush("#E6ECF6"), CornerRadius = new CornerRadius(6), Padding = new Thickness(3, 12, 3, 12), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4, 5, 4, 0) };
        var back = new Border { Child = Label("后门", 12, "#536581"), Background = Brush("#E6ECF6"), CornerRadius = new CornerRadius(6), Padding = new Thickness(3, 12, 3, 12), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4, 0, 4, 5) };
        doors.Children.Add(front); doors.Children.Add(back);
        Grid.SetColumn(doors, doorColumn); Grid.SetRowSpan(doors, plan.Rows); Board.Children.Add(doors);
        boardWidth = 116 + plan.Columns * 148 + aisles.Length * 30;
        boardHeight = plan.Rows * 98;
        Board.Width = boardWidth; Board.Height = boardHeight;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyFit));
    }
    private void ApplyFit()
    {
        if (!fit || boardWidth <= 0 || BoardScroll.ActualWidth <= 0) return;
        var scale = Math.Clamp(Math.Min((BoardScroll.ActualWidth - 20) / boardWidth, (BoardScroll.ActualHeight - 20) / boardHeight), 0.25, 1.3);
        BoardScale.ScaleX = BoardScale.ScaleY = scale;
    }
    public void ShowSettings()
    {
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow();
        settingsWindow.Saved += async (_, _) =>
        {
            cached = LocalStore.ReadCache(App.Settings.ServerUrl);
            if (cached != null) Render(cached.Plan);
            else { Board.Children.Clear(); PlanTitle.Text = "座位表"; PlanStats.Text = "连接你的班级，随时查看座位"; EmptyState.Visibility = Visibility.Visible; EmptyTitle.Text = "正在获取座位表…"; UpdatedLabel.Text = ""; }
            Position();
            await Refresh();
        };
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // User close gestures collapse the panel; application shutdown bypasses this handler.
        e.Cancel = true;
        Collapse();
    }
    private void WindowKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Collapse(); }
    private async void RefreshClick(object sender, RoutedEventArgs e) => await Refresh();
    private void CollapseClick(object sender, RoutedEventArgs e) => Collapse();
    private void FitClick(object sender, RoutedEventArgs e) { fit = true; ApplyFit(); }
    private void ZoomOutClick(object sender, RoutedEventArgs e) { fit = false; BoardScale.ScaleX = BoardScale.ScaleY = Math.Clamp(BoardScale.ScaleX / 1.15, .25, 2); }
    private void ZoomInClick(object sender, RoutedEventArgs e) { fit = false; BoardScale.ScaleX = BoardScale.ScaleY = Math.Clamp(BoardScale.ScaleX * 1.15, .25, 2); }
    private void ExpandClick(object sender, RoutedEventArgs e) { expanded = !expanded; ((Button)sender).Content = expanded ? "还原" : "展开"; Position(); }
    private void BoardSizeChanged(object sender, SizeChangedEventArgs e) => ApplyFit();
    private void SettingsClick(object sender, RoutedEventArgs e) => ShowSettings();
    private void ManageClick(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(App.Settings.ServerUrl + "/config") { UseShellExecute = true }); }
        catch { ConnectionStatus.Text = "无法打开浏览器，请手动访问网站的 /config 页面。"; }
    }
    internal async Task CheckMotion()
    {
        Directory.CreateDirectory(App.SmokeFolder!);
        try
        {
            Open();
            var fixedLeft = Left;
            await Task.Delay(90);
            var beforeClose = MotionSlide.X;
            Collapse();
            if (Math.Abs(MotionSlide.X - beforeClose) > 0.1) throw new Exception("Close reversal jumped.");
            await Task.Delay(70);
            var beforeReopen = MotionSlide.X;
            Open();
            if (Math.Abs(MotionSlide.X - beforeReopen) > 0.1) throw new Exception("Open reversal jumped.");
            await Task.Delay(430);
            if (!IsVisible || closing || motionActive || Math.Abs(MotionSlide.X) > 0.01 || Math.Abs(MotionRoot.Opacity - 1) > 0.01 || MotionRoot.CacheMode != null)
                throw new Exception("Panel did not settle into its visible state.");
            if (Math.Abs(Left - fixedLeft) > 0.01) throw new Exception("Animation moved the native window.");
            Collapse();
            await Task.Delay(260);
            if (IsVisible || motionActive || closing) throw new Exception("Close did not hide the window.");
            Open();
            await Task.Delay(430);
            if (!IsVisible || Math.Abs(MotionScale.ScaleX - 1) > 0.01 || Math.Abs(MotionRoot.Opacity - 1) > 0.01)
                throw new Exception("Panel could not reopen after hiding.");
            File.WriteAllText(Path.Combine(App.SmokeFolder!, "motion-result.json"), JsonSerializer.Serialize(new
            { passed = true, checks = new[] { "close reversal continuity", "open reversal continuity", "settled visibility/opacity", "bitmap cache released", "native window remains fixed", "collapse hides", "reopen restores scale" } }, LocalStore.Json));
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(App.SmokeFolder!, "motion-result.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString() }, LocalStore.Json));
            Application.Current.Shutdown(1);
        }
    }
    private async Task SmokeCapture()
    {
        try
        {
            Directory.CreateDirectory(App.SmokeFolder!);
            // Let layout and the opening animation settle before rendering the actual WPF view.
            await Task.Delay(400);
            ApplyFit(); UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var suffix = App.CompactSmoke ? "-compact" : "";
            using (var stream = File.Create(Path.Combine(App.SmokeFolder!, "panel" + suffix + ".png"))) encoder.Save(stream);
            File.WriteAllText(Path.Combine(App.SmokeFolder!, "result" + suffix + ".json"), JsonSerializer.Serialize(new
            { loaded = cached != null, rows = cached?.Plan.Rows, columns = cached?.Plan.Columns, cards = cached?.Plan.Seats.Count,
              scale = BoardScale.ScaleX, width = ActualWidth, height = ActualHeight, cornerRadius = PanelSurface.CornerRadius.TopLeft, status = ConnectionStatus.Text }, LocalStore.Json));
            var settings = new SettingsWindow();
            settings.Show(); settings.UpdateLayout();
            var settingsBitmap = new RenderTargetBitmap((int)settings.ActualWidth, (int)settings.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            settingsBitmap.Render(settings);
            var settingsEncoder = new PngBitmapEncoder(); settingsEncoder.Frames.Add(BitmapFrame.Create(settingsBitmap));
            using (var stream = File.Create(Path.Combine(App.SmokeFolder!, "settings.png"))) settingsEncoder.Save(stream);
            settings.Close();
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(App.SmokeFolder!, "error.txt"), ex.ToString()); }
        Application.Current.Shutdown(cached != null ? 0 : 1);
    }
}
