using System;
using System.IO;
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
            // A closed local port proves saving appearance does not depend on the API.
            Settings.ServerUrl = "http://localhost:18764";
            var screenshot = new SettingsWindow(); screenshot.Show(); screenshot.UpdateLayout();
            SaveAppearanceImage(screenshot, "settings-appearance"); screenshot.VerifyCancelForSmoke();
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
                checks = new[] { "cancel preserves current appearance", "three offline style saves", "saved event updates actual launcher", "style dimensions", "right edge placement", "SVG resources load" } }, LocalStore.Json));
            Shutdown();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(SmokeFolder!, "appearance-result.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString() }, LocalStore.Json));
            Shutdown(1);
        }
    }
    private static void SaveAppearanceImage(FrameworkElement view, string name)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(SmokeFolder!, name + ".png")); encoder.Save(stream);
    }
}
