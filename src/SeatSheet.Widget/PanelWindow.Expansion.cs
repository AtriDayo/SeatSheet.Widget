using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace SeatSheet.Widget;

public partial class PanelWindow
{
    private bool resizeActive, pendingCollapse;
    private int resizeVersion;
    private Rect resizeFrom, resizeTo, resizeCurrent, resizeWork;
    private double radiusFrom, radiusTo, radiusCurrent;
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
        if (motionActive || closing || !IsVisible) return;
        if (!resizeActive)
        {
            UpdateLayout();
            resizeCurrent = CardBounds(expanded);
            radiusCurrent = PanelSurface.CornerRadius.TopLeft;
            resizeActive = true;
            resizeWork = WorkArea();
            // Keep the layered native window fixed. Only the live card is rearranged.
            MotionRoot.CacheMode = null;
            PanelShadow.Visibility = Visibility.Collapsed;
            PanelSurface.Margin = new Thickness(0);
        }
        resizeFrom = resizeCurrent;
        radiusFrom = radiusCurrent;
        expanded = !expanded;
        resizeTo = CardBounds(expanded);
        radiusTo = expanded ? 0 : FloatingRadius();
        ExpandButton.Label = expanded ? "还原" : "展开";
        ExpandButton.Icon = expanded ? "restore" : "expand";
        var version = ++resizeVersion;
        BeginAnimation(ExpansionProgressProperty, null);
        ExpansionProgress = 0;
        UpdateExpansionFrame();
        if (!App.Settings.DynamicResizeAnimation || (!SystemParameters.ClientAreaAnimation && !App.IsExpansionQa))
        {
            FinishExpansion();
            return;
        }
        var animation = MotionAnimation(0, 1, TimeSpan.FromMilliseconds(expanded ? 380 : 320), new KeySpline(.16, .80, .22, 1));
        // Layout animation uses the UI thread; bound its update rate on older classroom PCs.
        Timeline.SetDesiredFrameRate(animation, 30);
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
        MotionRoot.Width = resizeCurrent.Width;
        MotionRoot.Height = resizeCurrent.Height;
        MotionRoot.Margin = new Thickness(resizeCurrent.X - resizeWork.X, resizeCurrent.Y - resizeWork.Y, 0, 0);
        PanelSurface.CornerRadius = new CornerRadius(radiusCurrent);
        MotionRoot.Clip = new RectangleGeometry(new Rect(0, 0, resizeCurrent.Width, resizeCurrent.Height), radiusCurrent, radiusCurrent);
        UpdateLayout();
        ApplyFit();
    }
    private void FinishExpansion()
    {
        ++resizeVersion;
        // Hold the destination before removing the animation clock, including a reversal.
        resizeFrom = resizeTo;
        radiusFrom = radiusTo;
        BeginAnimation(ExpansionProgressProperty, null);
        ExpansionProgress = 1;
        UpdateExpansionFrame();
        resizeActive = false;
        MotionRoot.Clip = null;
        Position(force: true);
        UpdateLayout(); ApplyFit();
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
            App.Settings.DynamicResizeAnimation=true;
            var plan = new SeatPlan { Name = "示例班级 · 动态布局验证", Rows = 6, Columns = 8 };
            for (var i=0;i<48;i++) plan.Seats.Add(new Seat {Row=i/8,Column=i%8,Name=$"示例{i+1:00}"});
            Render(plan);
            Open(); await Task.Delay(450);
            var floating = WindowBounds(false);
            var host = new Rect(Left, Top, Width, Height);
            var fontSize = PlanTitle.FontSize;
            var buttonSize = ExpandButton.RenderSize;
            monitor = (_, _) =>
            {
                if (!resizeActive) return;
                checkedFrames++;
                if (Math.Abs(Left-host.Left)>.01 || Math.Abs(Top-host.Top)>.01 || Math.Abs(Width-host.Width)>.01 || Math.Abs(Height-host.Height)>.01)
                    frameError = "Native host moved during expansion.";
                if (MotionRoot.Visibility!=Visibility.Visible || MotionRoot.Opacity<.99 || MotionRoot.CacheMode!=null)
                    frameError = "Live content was hidden or replaced by a cached image.";
                if (Math.Abs(BoardScale.ScaleX-BoardScale.ScaleY)>.001 || Math.Abs(MotionScale.ScaleX-1)>.001 || Math.Abs(MotionScale.ScaleY-1)>.001)
                    frameError = "Content was stretched disproportionately.";
                if (PlanTitle.FontSize!=fontSize || Math.Abs(ExpandButton.ActualWidth-buttonSize.Width)>.1 || Math.Abs(ExpandButton.ActualHeight-buttonSize.Height)>.1)
                    frameError = "Live text or controls changed size during expansion.";
            };
            CompositionTarget.Rendering += monitor;
            ToggleExpansion(); await Task.Delay(90);
            if (!resizeActive || radiusCurrent>=FloatingRadius() || radiusCurrent<=0 || resizeCurrent.Width<=floating.Width-20)
                throw new Exception("Expansion did not interpolate geometry and radius.");
            SaveExpansionCapture("expansion-mid");
            var before = resizeCurrent;
            var radiusBefore = radiusCurrent;
            ToggleExpansion();
            if (Math.Abs(resizeCurrent.X-before.X)>.1 || Math.Abs(radiusCurrent-radiusBefore)>.1)
                throw new Exception("Expansion reversal jumped.");
            await Task.Delay(500);
            if (expanded || resizeActive || Math.Abs(MotionRoot.Width-floating.Width)>.1 || Math.Abs(PanelSurface.CornerRadius.TopLeft-FloatingRadius())>.1)
                throw new Exception("Restore did not settle.");
            ToggleExpansion(); await Task.Delay(500);
            var work = WorkArea();
            if (!expanded || resizeActive || Math.Abs(Width-work.Width)>.1 || Math.Abs(Height-work.Height)>.1 || PanelSurface.CornerRadius.TopLeft!=0)
                throw new Exception("Maximum state did not fill the work area.");
            SaveExpansionCapture("expanded");
            ToggleExpansion(); await Task.Delay(500); SaveExpansionCapture("restored");
            ToggleExpansion(); await Task.Delay(70); Collapse(); await Task.Delay(650);
            if (IsVisible || resizeActive || pendingCollapse) throw new Exception("Collapse during expansion failed.");
            Open(); await Task.Delay(450);
            if (!IsVisible || motionActive) throw new Exception("Could not reopen after expansion collapse.");
            App.Settings.DynamicResizeAnimation=false;
            if (expanded) ToggleExpansion();
            ToggleExpansion();
            if (resizeActive || !expanded) throw new Exception("Disabled animation did not switch immediately.");
            ToggleExpansion();
            if (resizeActive || expanded) throw new Exception("Disabled restore did not switch immediately.");
            if (frameError!=null) throw new Exception(frameError);
            if (checkedFrames<5) throw new Exception("Too few rendered transition frames were checked.");
            File.WriteAllText(Path.Combine(App.SmokeFolder!, "expansion-result.json"), JsonSerializer.Serialize(new {passed=true,compact=App.CompactSmoke,checkedFrames,
                checks=new[] {"live layout per frame","uniform board scaling","stable text and button sizes","continuous reverse","restore bounds","maximum fills work area","collapse during resize","reopen","fixed native host","disabled animation switches immediately"}},LocalStore.Json));
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(App.SmokeFolder!, "expansion-result.json"), JsonSerializer.Serialize(new {passed=false,error=ex.ToString()},LocalStore.Json));
            Application.Current.Shutdown(1);
        }
        finally {if(monitor!=null) CompositionTarget.Rendering-=monitor;}
    }
}
