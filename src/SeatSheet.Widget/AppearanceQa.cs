using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SeatSheet.Widget;

public partial class App
{
    private async Task CheckAppearance()
    {
        Directory.CreateDirectory(SmokeFolder!);
        try
        {
            var firstRun = new SettingsWindow(); firstRun.Show(); await Task.Delay(250);
            if (firstRun.Address.Text != "" || firstRun.ConnectionState.Text != "尚未配置网站")
                throw new Exception("First use prefilled a development address or missed the setup prompt.");
            SaveAppearanceImage(firstRun, "settings-first-use");
            firstRun.SettingsTabs.SelectedIndex = 2; await Task.Delay(80);
            if (firstRun.AboutVersion.Text != "桌面版本 " + ProductInfo.Version || ProductInfo.Version != "0.1.0-beta.2")
                throw new Exception("About did not show the compiled desktop version.");
            SaveAppearanceImage(firstRun, "settings-about");
            firstRun.Width = 480; firstRun.Height = 680; await Task.Delay(80);
            SaveAppearanceImage(firstRun, "settings-about-narrow");
            firstRun.VerifyCancelForSmoke();
            var emptySave = new SettingsWindow(); emptySave.Show();
            await emptySave.VerifyUnconfiguredForSmoke();
            await VerifyFirstConnectionAsync();
            // A closed local port proves saving appearance does not depend on the API.
            Settings.ServerUrl = "http://localhost:18764";
            var screenshot = new SettingsWindow(); screenshot.Show(); await Task.Delay(250); screenshot.UpdateLayout();
            SaveAppearanceImage(screenshot, "settings-appearance");
            screenshot.GeneralSettingsScroll.ScrollToBottom(); await Task.Delay(80);
            SaveAppearanceImage(screenshot, "settings-resize-animation"); screenshot.VerifyCancelForSmoke();
            foreach (var kind in new[] { LauncherStyles.Label, LauncherStyles.Slim, LauncherStyles.Arrow })
            {
                var candidate = new SettingsWindow();
                candidate.Saved += (_, _) => launcher!.ApplyAppearance();
                candidate.Show();
                await candidate.VerifyAppearanceForSmoke(kind);
                launcher!.UpdateLayout();
                var expected = kind == LauncherStyles.Label ? new Size(30, 72) : kind == LauncherStyles.Slim ? new Size(12, 56) : new Size(24, 48);
                if (launcher.Width != expected.Width || launcher.Height != expected.Height ||
                    Math.Abs(launcher.Left + launcher.Width - SystemParameters.WorkArea.Right) > .1)
                    throw new Exception("Saved handle style did not update the launcher or its edge position.");
                SaveAppearanceImage(launcher, "launcher-" + kind);
            }
            panel!.Open(); await Task.Delay(400); SaveAppearanceImage(panel, "panel-vector-icons");
            File.WriteAllText(Path.Combine(SmokeFolder!, "appearance-result.json"), JsonSerializer.Serialize(new { passed = true,
                checks = new[] { "blank first-use address and setup prompt", "about uses compiled desktop version", "separate product download cards", "failed initial connection preserves blank configuration", "unconfigured appearance saves offline", "successful first connection tests without saving then validates on save", "cancel preserves current appearance", "three offline style saves", "saved event updates actual launcher", "style dimensions", "right edge placement", "SVG resources load" } }, LocalStore.Json));
            Shutdown();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(SmokeFolder!, "appearance-result.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString() }, LocalStore.Json));
            Shutdown(1);
        }
    }
    private static async Task VerifyFirstConnectionAsync()
    {
        // Serve synthetic data on an ephemeral loopback port, never a classroom website.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var source = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        var responseTask = Task.Run(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                var request = await reader.ReadLineAsync();
                if (request != "GET /api/seat-plan HTTP/1.1") throw new Exception("Unexpected first-use request.");
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new SeatPlan { Name = "示例班级", Rows = 2, Columns = 3 }));
                var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header); await stream.WriteAsync(body);
            }
        });
        try
        {
            var settings = new SettingsWindow(); settings.Show();
            await settings.VerifyFirstConnectionForSmoke(source);
            await responseTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { listener.Stop(); }
    }
    internal static void SaveAppearanceImage(FrameworkElement view, string name)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(SmokeFolder!, name + ".png")); encoder.Save(stream);
    }
}
