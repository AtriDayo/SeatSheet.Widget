using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace SeatSheet.Widget;
public partial class SettingsWindow : Window
{
    private bool closed;
    public event EventHandler? Saved;
    public SettingsWindow()
    {
        InitializeComponent();
        Address.Text = App.Settings.ServerUrl;
        PanelWidth.Value = App.Settings.PanelWidth;
        Closed += (_, _) => closed = true;
    }
    private async void SaveClick(object sender, RoutedEventArgs e) => await CheckConnection(true);
    private async void TestClick(object sender, RoutedEventArgs e) => await CheckConnection(false);
    private async Task CheckConnection(bool save)
    {
        SaveButton.IsEnabled = TestButton.IsEnabled = Address.IsEnabled = PanelWidth.IsEnabled = CancelButton.IsEnabled = false;
        ConnectionProgress.Visibility = Visibility.Visible;
        ConnectionState.Text = "正在连接…";
        ConnectionState.Foreground = (System.Windows.Media.Brush)FindResource("Muted");
        Message.Text = "正在获取座位表，请稍候。";
        try
        {
            var source = SeatApi.Normalize(Address.Text);
            var result = await SeatApi.Fetch(source);
            if (closed) return;
            Address.Text = source;
            ConnectionState.Text = "连接成功";
            ConnectionState.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 104, 73));
            Message.Text = $"{result.Plan.Name} · {result.Plan.Rows} 排 × {result.Plan.Columns} 列";
            if (!save) return;
            var oldSource = App.Settings.ServerUrl;
            var oldWidth = App.Settings.PanelWidth;
            App.Settings.ServerUrl = source;
            App.Settings.PanelWidth = PanelWidth.Value;
            try { LocalStore.SaveSettings(); }
            catch { App.Settings.ServerUrl = oldSource; App.Settings.PanelWidth = oldWidth; throw; }
            try { LocalStore.SaveCache(result); } catch { /* The live connection remains usable. */ }
            Saved?.Invoke(this, EventArgs.Empty);
            Close();
        }
        catch (Exception ex)
        {
            ConnectionState.Text = "连接或保存失败";
            ConnectionState.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 65, 47));
            Message.Text = ex switch
            {
                ArgumentException => ex.Message,
                TaskCanceledException => "连接超时，请检查网络或网站是否可用。",
                HttpRequestException => "无法连接，请检查网站地址与网络。现有设置已保留。",
                _ => "请检查地址是否为 SeatSheet 网站，以及本地保存权限。"
            };
        }
        finally
        {
            SaveButton.IsEnabled = TestButton.IsEnabled = Address.IsEnabled = PanelWidth.IsEnabled = CancelButton.IsEnabled = true;
            ConnectionProgress.Visibility = Visibility.Collapsed;
        }
    }
    private void WidthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WidthValue == null || WidthHint == null) return;
        WidthValue.Text = $"{e.NewValue:0}";
        var work = SystemParameters.WorkArea;
        var gap = Math.Clamp(Math.Min(work.Width, work.Height) * .02, 12, 24);
        var available = Math.Max(1, work.Width - gap * 2);
        var minimum = Math.Min(480, available);
        var actual = Math.Clamp(e.NewValue, minimum, Math.Max(minimum, available * .72));
        WidthHint.Text = $"当前屏幕显示宽度约 {actual:0}，会自动限制以留出桌面空间。展开模式不受此设置影响。";
    }
    private void CancelClick(object sender, RoutedEventArgs e) => Close();
    internal async Task VerifyConnectionForSmoke()
    {
        var source = App.Settings.ServerUrl;
        var width = App.Settings.PanelWidth;
        await CheckConnection(false);
        if (ConnectionState.Text != "连接成功" || source != App.Settings.ServerUrl || width != App.Settings.PanelWidth)
            throw new InvalidOperationException("Settings connection test failed or changed saved configuration.");
        if (Icon == null) throw new InvalidOperationException("The settings window icon was not loaded.");
    }
}
