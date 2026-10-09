using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace SeatSheet.Widget;

public partial class PanelWindow
{
    private bool resizeActive, pendingCollapse, handoffActive;
    private int resizeVersion;
    private Rect resizeFrom, resizeTo, resizeCurrent, resizeWork;
    private double radiusFrom, radiusTo, radiusCurrent;
    private Rect snapshotButton;
    private double snapshotWidth, snapshotHeight;
    private static readonly DependencyProperty ExpansionProgressProperty = DependencyProperty.Register(
        "ExpansionProgress", typeof(double), typeof(PanelWindow), new PropertyMetadata(0.0, (owner, _) => ((PanelWindow)owner).UpdateExpansionFrame()));
    private double ExpansionProgress { get => (double)GetValue(ExpansionProgressProperty); set => SetValue(ExpansionProgressProperty, value); }
    private Rect WorkArea()
    {
        var work = SystemParameters.WorkArea;
        return App.IsSmoke && App.CompactSmoke ? new Rect(work.Right - 1366, work.Top, 1366, 720) : work;
    }
    private double FloatingRadius() => Math.Clamp(Math.Min(WorkArea().Width, WorkArea().Height) * .018, 12, 24);
    private Rect WindowBounds(bool large)
    {
        var work = WorkArea();
        if (large) return work;
        var gap = Math.Clamp(Math.Min(work.Width, work.Height) * .02, 12, 24);
        var available = Math.Max(1, work.Width - gap * 2);
        var minimum = Math.Min(480, available);
        var width = Math.Clamp(App.Settings.PanelWidth, minimum, Math.Max(minimum, available * .72));
        var height = Math.Min(720, work.Height * .80);
        return new Rect(work.Right - width - Math.Max(gap, 36), work.Top + (work.Height - height) / 2, width, height);
    }
    private Rect CardBounds(bool large)
    {
        var rect = WindowBounds(large);
        if (!large) rect.Inflate(-10, -10);
        return rect;
    }
    private void ToggleExpansion()
    {
        if (motionActive || closing || handoffActive || !IsVisible) return;
        if (!resizeActive)
        {
            UpdateLayout();
            resizeCurrent = CardBounds(expanded);
            radiusCurrent = PanelSurface.CornerRadius.TopLeft;
            snapshotWidth = PanelSurface.ActualWidth;
            snapshotHeight = PanelSurface.ActualHeight;
            snapshotButton = new Rect(ExpandButton.TranslatePoint(new Point(), PanelSurface), ExpandButton.RenderSize);
            // Draw square backing plus the content at its actual offset; the live clip owns the corners.
            // Rendering the Border directly would bake its old rounded corners and visual offset into the image.
            var dpi = VisualTreeHelper.GetDpi(this);
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(snapshotWidth * dpi.DpiScaleX), (int)Math.Ceiling(snapshotHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen())
            {
                context.DrawRectangle(PanelSurface.Background, new Pen(PanelSurface.BorderBrush, 1), new Rect(0, 0, snapshotWidth, snapshotHeight));
                var contentOrigin = PanelContent.TranslatePoint(new Point(), PanelSurface);
                context.DrawRectangle(new VisualBrush(PanelContent) { Stretch = Stretch.Fill }, null, new Rect(contentOrigin, PanelContent.RenderSize));
            }
            bitmap.Render(drawing); bitmap.Freeze();
            ExpansionImage.Source = bitmap;
            resizeActive = true;
            resizeWork = WorkArea();
            ExpansionLayer.BeginAnimation(OpacityProperty, null);
            ExpansionLayer.Opacity = 1;
            ExpansionLayer.IsHitTestVisible = true;
            ExpansionToggle.Visibility = Visibility.Visible;
            ExpansionLayer.Visibility = Visibility.Visible;
        }
        resizeFrom = resizeCurrent;
        radiusFrom = radiusCurrent;
        expanded = !expanded;
        resizeTo = CardBounds(expanded);
        radiusTo = expanded ? 0 : FloatingRadius();
        ExpansionToggle.Content = ExpandButton.Content = expanded ? "还原" : "展开";
        var version = ++resizeVersion;
        BeginAnimation(ExpansionProgressProperty, null);
        ExpansionProgress = 0;
        // Install the complete first frame before hiding live content, including on reversal.
        UpdateExpansionFrame();
        UpdateLayout();
        MotionRoot.Visibility = Visibility.Hidden;
        var duration = TimeSpan.FromMilliseconds(expanded ? 380 : 320);
        var animation = MotionAnimation(0, 1, duration, new KeySpline(.16, .80, .22, 1));
        animation.Completed += (_, _) => { if (version == resizeVersion) FinishExpansion(); };
        BeginAnimation(ExpansionProgressProperty, animation);
    }
    private void UpdateExpansionFrame()
    {
        if (!resizeActive) return;
        var p = ExpansionProgress;
        static double Mix(double from, double to, double progress) => from + (to - from) * progress;
        resizeCurrent = new Rect(Mix(resizeFrom.X, resizeTo.X, p), Mix(resizeFrom.Y, resizeTo.Y, p), Mix(resizeFrom.Width, resizeTo.Width, p), Mix(resizeFrom.Height, resizeTo.Height, p));
        radiusCurrent = Mix(radiusFrom, radiusTo, p);
        Canvas.SetLeft(ExpansionCard, resizeCurrent.X - resizeWork.X); Canvas.SetTop(ExpansionCard, resizeCurrent.Y - resizeWork.Y);
        ExpansionCard.Width = resizeCurrent.Width; ExpansionCard.Height = resizeCurrent.Height;
        ExpansionCard.Clip = new RectangleGeometry(new Rect(0, 0, resizeCurrent.Width, resizeCurrent.Height), radiusCurrent, radiusCurrent);
        var sx = resizeCurrent.Width / snapshotWidth; var sy = resizeCurrent.Height / snapshotHeight;
        Canvas.SetLeft(ExpansionToggle, resizeCurrent.X - resizeWork.X + snapshotButton.X * sx);
        Canvas.SetTop(ExpansionToggle, resizeCurrent.Y - resizeWork.Y + snapshotButton.Y * sy);
        ExpansionToggle.Width = snapshotButton.Width * sx; ExpansionToggle.Height = snapshotButton.Height * sy;
        ExpansionToggle.FontSize = 12 * Math.Min(sx, sy);
    }
    private void FinishExpansion(bool immediate = false)
    {
        if (handoffActive && !immediate) return;
        var version = ++resizeVersion;
        // Removing a HoldEnd clock otherwise resets progress to its base value (zero).
        // Hold the current geometry while the live layout is prepared behind the snapshot.
        resizeFrom = resizeTo;
        radiusFrom = radiusTo;
        BeginAnimation(ExpansionProgressProperty, null);
        ExpansionProgress = 1;
        UpdateExpansionFrame();
        handoffActive = true;
        Position(force: true);
        MotionRoot.Visibility = Visibility.Visible;
        UpdateLayout(); ApplyFit(); UpdateLayout();
        ExpansionLayer.IsHitTestVisible = false;
        ExpansionToggle.Visibility = Visibility.Collapsed;
        ExpansionLayer.BeginAnimation(OpacityProperty, null);
        ExpansionLayer.Opacity = 1;
        if (immediate) { CompleteExpansionHandoff(version); return; }
        // Keep the opaque final animation frame until the freshly arranged live view
        // is available, then blend to sharp text without a blank frame or layout jump.
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(80));
        fade.Completed += (_, _) => CompleteExpansionHandoff(version);
        ExpansionLayer.BeginAnimation(OpacityProperty, fade);
    }
    private void CompleteExpansionHandoff(int version)
    {
        if (version != resizeVersion) return;
        ExpansionLayer.Visibility = Visibility.Collapsed;
        ExpansionLayer.BeginAnimation(OpacityProperty, null);
        ExpansionImage.Source = null;
        handoffActive = resizeActive = false;
        if (pendingCollapse) { pendingCollapse = false; Collapse(); }
    }
    private void SaveExpansionCapture(string name)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(App.SmokeFolder!, name + ".png")); encoder.Save(stream);
    }
    internal async Task CheckExpansion()
    {
        Directory.CreateDirectory(App.SmokeFolder!);
        string? frameError = null;
        var checkedFrames = 0;
        EventHandler? monitor = null;
        try
        {
            try { cached = await SeatApi.Fetch(App.Settings.ServerUrl); Render(cached.Plan); Status("已连接 · 动画验证", true); }
            catch { Render(new SeatPlan { Name = "动画测试", Rows = 6, Columns = 8 }); }
            Open(); await Task.Delay(450);
            var floating = WindowBounds(false);
            var host = new Rect(Left, Top, Width, Height);
            monitor = (_, _) =>
            {
                if (!resizeActive) return;
                checkedFrames++;
                if (Math.Abs(Left - host.Left) > .01 || Math.Abs(Top - host.Top) > .01 ||
                    Math.Abs(Width - host.Width) > .01 || Math.Abs(Height - host.Height) > .01)
                    frameError = $"Native host moved or resized during expansion: {host} -> {new Rect(Left, Top, Width, Height)}, handoff={handoffActive}.";
                if (MotionRoot.Visibility != Visibility.Visible &&
                    (ExpansionLayer.Visibility != Visibility.Visible || ExpansionImage.Source == null || ExpansionLayer.Opacity < .99))
                    frameError = "A frame exposed neither live content nor an opaque snapshot.";
                if (handoffActive && (Math.Abs(resizeCurrent.Width - resizeTo.Width) > .1 || Math.Abs(radiusCurrent - radiusTo) > .1))
                    frameError = "Removing the animation clock reset its final geometry.";
            };
            CompositionTarget.Rendering += monitor;
            ToggleExpansion(); await Task.Delay(90);
            if (!resizeActive || radiusCurrent >= FloatingRadius() || radiusCurrent <= 0 || resizeCurrent.Width <= floating.Width - 20)
                throw new Exception("Expansion did not interpolate geometry and radius.");
            SaveExpansionCapture("expansion-mid");
            var before = resizeCurrent;
            var radiusBefore = radiusCurrent;
            ToggleExpansion();
            if (Math.Abs(resizeCurrent.X - before.X) > .1 || Math.Abs(radiusCurrent - radiusBefore) > .1)
                throw new Exception("Expansion reversal jumped.");
            await Task.Delay(500);
            if (expanded || resizeActive || Math.Abs(MotionRoot.Width - floating.Width) > .1 || Math.Abs(PanelSurface.CornerRadius.TopLeft - FloatingRadius()) > .1)
                throw new Exception("Restore did not settle.");
            ToggleExpansion(); await Task.Delay(500);
            var work = WorkArea();
            if (!expanded || resizeActive || Math.Abs(Width - work.Width) > .1 || Math.Abs(Height - work.Height) > .1 || PanelSurface.CornerRadius.TopLeft != 0 || ExpansionImage.Source != null)
                throw new Exception("Maximum state did not fill the work area and release snapshot.");
            SaveExpansionCapture("expanded");
            ToggleExpansion(); await Task.Delay(500); SaveExpansionCapture("restored");
            ToggleExpansion(); await Task.Delay(70); Collapse(); await Task.Delay(650);
            if (IsVisible || resizeActive || pendingCollapse) throw new Exception("Collapse during expansion failed.");
            Open(); await Task.Delay(450);
            if (!IsVisible || motionActive) throw new Exception("Could not reopen after expansion collapse.");
            if (frameError != null) throw new Exception(frameError);
            if (checkedFrames < 5) throw new Exception("Too few rendered transition frames were checked.");
            File.WriteAllText(Path.Combine(App.SmokeFolder!, "expansion-result.json"), JsonSerializer.Serialize(new { passed = true, compact = App.CompactSmoke, checkedFrames,
                checks = new[] { "intermediate dimensions", "progressive corner radius", "continuous reverse", "restore bounds", "maximum fills work area", "snapshot released", "collapse during resize", "reopen", "fixed native host per frame", "no uncovered transition frames", "final geometry retained during handoff" } }, LocalStore.Json));
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(App.SmokeFolder!, "expansion-result.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString() }, LocalStore.Json));
            Application.Current.Shutdown(1);
        }
        finally { if (monitor != null) CompositionTarget.Rendering -= monitor; }
    }
}
