using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SeatSheet.Widget;

public partial class SettingsWindow
{
    private bool allowAnimatedClose, closingMotion, verifyCloseMotion;
    private void InitializeWindowMotion()
    {
        var animate = SystemParameters.ClientAreaAnimation;
        SettingsSurface.Opacity = animate ? 0 : 1;
        SettingsSlide.Y = animate ? 6 : 0;
        Loaded += (_, _) =>
        {
            if (!animate) return;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            SettingsSurface.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            SettingsSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        };
        Closing += (_, e) =>
        {
            if (allowAnimatedClose || !IsLoaded || !animate || (App.IsSmoke && !verifyCloseMotion)) return;
            e.Cancel = true;
            if (closingMotion) return;
            closingMotion = true;
            closed = true;
            SettingsSurface.IsHitTestVisible = false;
            var fade = new DoubleAnimation(SettingsSurface.Opacity, 0, TimeSpan.FromMilliseconds(120));
            fade.Completed += (_, _) => { allowAnimatedClose = true; Close(); };
            SettingsSurface.BeginAnimation(OpacityProperty, fade);
            SettingsSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(SettingsSlide.Y, 4, TimeSpan.FromMilliseconds(120)));
        };
    }
    internal async Task VerifyMotionForSmoke()
    {
        if (!App.IsSmoke) throw new InvalidOperationException("Requires isolated QA.");
        await Task.Delay(250);
        if (Math.Abs(SettingsSurface.Opacity - 1) > .01 || Math.Abs(SettingsSlide.Y) > .01)
            throw new InvalidOperationException("Settings opening transition did not settle.");
        verifyCloseMotion = true;
        Close();
        await Task.Delay(180);
        if (IsVisible) throw new InvalidOperationException("Settings closing transition did not close the window.");
    }
}
