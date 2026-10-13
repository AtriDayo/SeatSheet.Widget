using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SeatSheet.Widget;

public partial class App
{
    private async Task CheckSettingsTabMotion()
    {
        Directory.CreateDirectory(SmokeFolder!);
        try
        {
            var settings = new SettingsWindow(new CachedPlan { Source = "http://localhost:18764", Plan = new SeatPlan { Name = "示例班级", Rows = 2, Columns = 3 } });
            settings.Show(); await Task.Delay(260);
            await settings.VerifyTabMotionForSmoke();
            File.WriteAllText(Path.Combine(SmokeFolder!, "tab-motion-result.json"), JsonSerializer.Serialize(new { passed = true, checks = new[] {
                "staggered side exit", "top-down entrance", "visible cards only", "last rapid selection wins", "controls retain drafts and scroll", "disabled motion switches immediately", "close releases clocks and caches", "no animation transforms remain", "independent effects cancel safely" } }, LocalStore.Json));
            Shutdown();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(SmokeFolder!, "tab-motion-result.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString() }, LocalStore.Json));
            Shutdown(1);
        }
    }
}

public partial class SettingsWindow
{
    internal async Task VerifyTabMotionForSmoke()
    {
        if (!App.IsSmoke) throw new InvalidOperationException("Requires isolated motion QA.");
        var originalOptions = JsonSerializer.Serialize(App.Settings);
        Address.Text = "https://seats.example.com";
        TabAnimationOption.IsChecked = true;
        var systemMotion = SystemParameters.ClientAreaAnimation;
        SettingsTabs.SelectedIndex = 2;
        if (systemMotion)
        {
            await Task.Delay(55);
            if (!tabTransitionRunning || movingCards.Count == 0 || movingCards[0].Offset.X >= 0 || movingCards[0].Element.Opacity >= 1)
                throw new Exception("Old cards did not slide out individually.");
            if (movingCards.Count > 1 && movingCards[0].Element.Opacity >= movingCards[1].Element.Opacity)
                throw new Exception("Card exit order was not staggered.");
            App.SaveAppearanceImage(this, "settings-tabs-exit");
            await Task.Delay(135);
            if (shownTab != 2 || !tabTransitionRunning || movingCards.Count != 3 || movingCards[0].Offset.Y >= 0)
                throw new Exception("New cards did not enter from above.");
            App.SaveAppearanceImage(this, "settings-tabs-enter");
        }
        await tabMotionTask;
        if (shownTab != 2 || movingCards.Count != 0 || tabPresenter?.IsHitTestVisible != true)
            throw new Exception("Tab animation did not settle or release resources.");
        SettingsTabs.SelectedIndex = 0; await tabMotionTask;
        GeneralSettingsScroll.ScrollToBottom(); await Task.Delay(40);
        var scroll = GeneralSettingsScroll.VerticalOffset;
        App.SaveAppearanceImage(this, "settings-visual-effects");
        SettingsTabs.SelectedIndex = 1;
        await Task.Delay(25); SettingsTabs.SelectedIndex = 2;
        await Task.Delay(25); SettingsTabs.SelectedIndex = 0;
        await tabMotionTask;
        if (shownTab != 0 || Address.Text != "https://seats.example.com" || Math.Abs(GeneralSettingsScroll.VerticalOffset - scroll) > 1)
            throw new Exception("Rapid selection lost the desired page, draft or scroll position.");
        foreach (var card in VisibleTabCards())
            if (card.CacheMode != null || !card.RenderTransform.Value.IsIdentity || card.Opacity != 1)
                throw new Exception("Finished motion left a cache or transform behind.");
        SettingsTabs.SelectedIndex = 2; await Task.Delay(30);
        TabAnimationOption.IsChecked = false;
        if (tabTransitionRunning || shownTab != 2 || movingCards.Count != 0)
            throw new Exception("Disabling motion during a transition did not settle immediately.");
        SettingsTabs.SelectedIndex = 1;
        if (shownTab != 1 || tabTransitionRunning) throw new Exception("Disabled motion still delayed page selection.");
        TabAnimationOption.IsChecked = true;
        Width = 480; Height = 680;
        SettingsTabs.SelectedIndex = 2; await tabMotionTask;
        App.SaveAppearanceImage(this, "settings-tabs-narrow");
        if (shownTab != 2 || movingCards.Count != 0 || tabStage?.ActualWidth <= 0)
            throw new Exception("Narrow tab motion did not settle within its viewport.");
        SettingsTabs.SelectedIndex = 0; await Task.Delay(25);
        Close(); await Task.Delay(170);
        if (movingCards.Count != 0 || tabTransitionRunning || IsVisible || JsonSerializer.Serialize(App.Settings) != originalOptions)
            throw new Exception("Closing during motion leaked resources or saved draft preferences.");
    }
}
