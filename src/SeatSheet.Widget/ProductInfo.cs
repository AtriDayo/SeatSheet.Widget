using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace SeatSheet.Widget;

internal static class ProductInfo
{
    internal static string Version => typeof(ProductInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "未知";
    // Explicit product links also work for prereleases; GitHub's latest endpoint is shared by the repository.
    internal const string WidgetRelease = "https://github.com/AtriDayo/SeatSheet.Widget/releases/tag/widget-v0.1.0-beta.2";
    internal const string PluginRelease = "https://github.com/AtriDayo/SeatSheet.Widget/releases/tag/plugin-v0.1.5";
}

public partial class SettingsWindow
{
    private void WidgetDownloadClick(object sender, RoutedEventArgs e) => OpenRelease(ProductInfo.WidgetRelease);
    private void PluginDownloadClick(object sender, RoutedEventArgs e) => OpenRelease(ProductInfo.PluginRelease);
    private void OpenRelease(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception)
        {
            AboutMessage.Text = "无法打开浏览器，请手动访问：" + url;
            AboutMessage.Visibility = Visibility.Visible;
        }
    }
}
