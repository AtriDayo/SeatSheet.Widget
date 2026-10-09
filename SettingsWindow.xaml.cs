using System;
using System.Windows;

namespace SeatSheet.Widget;
public partial class SettingsWindow : Window
{
    public event EventHandler? Saved;
    public SettingsWindow()
    {
        InitializeComponent();
        Address.Text = App.Settings.ServerUrl;
        PanelWidth.Value = App.Settings.PanelWidth;
    }
    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        Message.Text = "正在验证连接…";
        try
        {
            var source = SeatApi.Normalize(Address.Text);
            var result = await SeatApi.Fetch(source);
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
            Message.Text = ex is ArgumentException ? ex.Message : "连接或保存失败，请检查地址、网络和本地写入权限。";
        }
        finally { SaveButton.IsEnabled = true; }
    }
    private void CancelClick(object sender, RoutedEventArgs e) => Close();
}
