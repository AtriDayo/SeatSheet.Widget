using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SeatSheet.RollCall.Protocol;

namespace SeatSheet.Widget;

public partial class PanelWindow
{
    internal async Task CheckRollCallUiAsync()
    {
        Directory.CreateDirectory(App.SmokeFolder!);
        try
        {
            cached = new CachedPlan { Source = App.Settings.ServerUrl, FetchedAt=DateTimeOffset.Now, Plan = new SeatPlan { Name = "示例班级", Rows = 2, Columns = 3, UpdatedAt=DateTimeOffset.UtcNow,
                Seats = new() { new() { Name="示例甲",StudentNo="01" },new() { Name="示例乙",StudentNo="02",Column=1 },
                    new() {Name="示例丙",StudentNo="03",Row=1},new() {Row=1,Column=1,Name=" "} } } };
            // Use a full classroom, not three rows, to expose scrollbar and alignment problems.
            cached.Plan.Rows = 8;
            cached.Plan.Columns = 6;
            for (var i=0;i<48;i++)
            {
                var row=i/6; var column=i%6;
                if (cached.Plan.Seats.Exists(s=>s.Row==row&&s.Column==column)) continue;
                cached.Plan.Seats.Add(new Seat {Row=row,Column=column,Name=$"示例同学{i+1:00}",StudentNo=i%3==0 ? "" : $"{i+1:00}"});
            }
            var motion = new SettingsWindow(cached); motion.Show(); await motion.VerifyMotionForSmoke();
            var canceled = new SettingsWindow(cached); canceled.Show(); await canceled.VerifyRollCallDraftForSmoke(false);
            var settings = new SettingsWindow(cached); settings.Show(); settings.SettingsTabs.SelectedIndex=1;
            settings.RollCallOutput.SelectedIndex=1;
            if (!settings.ProbePluginButton.IsEnabled || settings.PluginConnection.Text.Contains("无需连接"))
                throw new InvalidOperationException("Draft external output did not update connection controls.");
            await Task.Delay(250); CaptureRollCallView(settings,"rollcall-external-settings");
            settings.RollCallOutput.SelectedIndex=0;
            if (settings.ProbePluginButton.IsEnabled) throw new InvalidOperationException("Local output still enables plugin probing.");
            settings.NoRepeatOption.IsChecked=true;
            await Task.Delay(250); CaptureRollCallView(settings,"rollcall-settings");
            CaptureRollCallView(settings,"rollcall-settings-125",1.25);
            CaptureRollCallView(settings,"rollcall-settings-150",1.5);
            settings.Width=480; settings.Height=680; await Task.Delay(80); CaptureRollCallView(settings,"rollcall-settings-narrow");
            settings.SettingsTabs.SelectedIndex=0; CaptureRollCallView(settings,"general-settings-narrow");
            settings.Width=860; settings.Height=850; await Task.Delay(80); CaptureRollCallView(settings,"general-settings");
            settings.SettingsTabs.SelectedIndex=1;
            await settings.VerifyRollCallDraftForSmoke(true);
            Render(cached.Plan);
            Position(); MotionRoot.Opacity=1; Show(); await Task.Delay(80); UpdateLayout(); ApplyFit();
            Status("已连接 · 打开面板时自动刷新",true);
            if (UpdatedLabel.Parent != PlanMetadata || !ConnectionStatus.Text.StartsWith("已连接"))
                throw new InvalidOperationException("Panel metadata or compact connection summary is misplaced.");
            CaptureRollCallView(PanelSurface,"classroom-panel");
            var previousWidth=App.Settings.PanelWidth;
            App.Settings.PanelWidth=480; Position(); await Task.Delay(80); UpdateLayout(); ApplyFit();
            PlanTitle.Text="示例班级：用于检查较长标题是否挤压工具栏";
            CaptureRollCallView(PanelSurface,"classroom-panel-narrow");
            App.Settings.PanelWidth=previousWidth; Position(); Render(cached.Plan);
            await DrawStudentAsync();
            if(lastRollCall?.Student?.Name=="示例乙" || resultWindow==null) throw new InvalidOperationException("Absent student or missing local result.");
            CaptureRollCallView(resultWindow,"rollcall-result");
            resultWindow.Close();
            App.UseIsolatedNotificationsForQa();
            App.Settings.RollCall.Output = "classIsland";
            App.RollCall.ResetRound();
            await DrawStudentAsync();
            if (resultWindow == null) throw new InvalidOperationException("Failed external delivery did not show the local result.");
            var originalId = lastRollCall!.MessageId;
            await RetryRollCallAsync();
            if (lastRollCall.MessageId != originalId) throw new InvalidOperationException("Retry redrew the student.");
            if (!ConnectionStatus.Text.StartsWith("已连接") || RollCallStatus.Text.Length==0)
                throw new InvalidOperationException("Delivery warning replaced the connection summary or lost its detail.");
            if (resultWindow.ResultSurface.CornerRadius.TopLeft!=18 || !resultWindow.AllowsTransparency || System.Windows.Shell.WindowChrome.GetWindowChrome(resultWindow)!=null)
                throw new InvalidOperationException("Result window did not use transparent rounded corners.");
            CaptureRollCallView(resultWindow,"rollcall-fallback");
            resultWindow.SetResult(new RollCallMessage { Student=new RollCallStudent {Name="示例同学长姓名布局检查", ClassName="示例班级：用于检查较长班级名称的显示",Seat=new RollCallSeat {Row=8,Column=6}}},true);
            resultWindow.SetStatus("通知发送失败：ClassIsland 暂不可用。已在本软件显示；重发此结果不会重新抽人。");
            CaptureRollCallView(resultWindow,"rollcall-long-text");
            resultWindow.Close();
            File.WriteAllText(Path.Combine(App.SmokeFolder!,"rollcall-result.json"),JsonSerializer.Serialize(new {passed=true,
                checks=new[] {"settings opening and closing settle","full classroom at three render scales","wide and narrow layout snapshots","long result text snapshot","draft output updates connection controls","settings cancel preserves draft","offline settings save","empty seat filtering","absent student exclusion","transparent rounded local result window","compact connection summary","metadata beside row and column counts","external failure falls back to local","retry preserves result ID"}}));
            Application.Current.Shutdown();
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(App.SmokeFolder!,"rollcall-result.json"),JsonSerializer.Serialize(new {passed=false,error=ex.ToString()}));
            Application.Current.Shutdown(1);
        }
    }
    private static void CaptureRollCallView(FrameworkElement view,string name,double scale=1)
    {
        view.UpdateLayout();
        var image=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth*scale),(int)Math.Ceiling(view.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);
        if (view.Parent != null)
        {
            var drawing=new DrawingVisual();
            var bounds=new Rect(0,0,view.ActualWidth,view.ActualHeight);
            var offset=VisualTreeHelper.GetOffset(view);
            using (var context=drawing.RenderOpen()) context.DrawRectangle(new VisualBrush(view)
                { ViewboxUnits=BrushMappingMode.Absolute, Viewbox=new Rect(offset.X,offset.Y,view.ActualWidth,view.ActualHeight), Stretch=Stretch.Fill },null,bounds);
            image.Render(drawing);
        }
        else image.Render(view);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file=File.Create(Path.Combine(App.SmokeFolder!,name+".png")); encoder.Save(file);
    }
}
