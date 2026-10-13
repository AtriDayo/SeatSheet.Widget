using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SeatSheet.Widget;

public partial class PanelWindow
{
    private bool breakBlocked, checkingAvailability;
    private readonly DispatcherTimer lessonTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool HasRollCallView => IsVisible || floatingWindow?.IsVisible == true || resultWindow?.IsVisible == true;
    private void InitializeRollCallAvailability()
    {
        lessonTimer.Tick += async (_, _) =>
        {
            if (!App.Settings.RollCall.DisableDuringBreak || !HasRollCallView) { SynchronizeRollCallAvailability(); return; }
            await RefreshRollCallAvailabilityAsync();
        };
        IsVisibleChanged += (_, _) => SynchronizeRollCallAvailability();
    }
    private void SynchronizeRollCallAvailability()
    {
        if (!App.Settings.RollCall.DisableDuringBreak)
        {
            lessonTimer.Stop(); breakBlocked = false; UpdateRollCallButtons(); return;
        }
        if (!HasRollCallView) { lessonTimer.Stop(); return; }
        UpdateRollCallButtons();
        if (!App.IsSmoke) { lessonTimer.Start(); _ = RefreshRollCallAvailabilityAsync(); }
    }
    private async Task RefreshRollCallAvailabilityAsync()
    {
        if (checkingAvailability) return;
        checkingAvailability = true;
        try
        {
            LessonAvailability state;
            try { state = await App.LessonProbe(); } catch { state = LessonAvailability.Unknown; }
            if (!Dispatcher.HasShutdownStarted) ApplyCourseAvailability(state);
        }
        finally { checkingAvailability = false; }
    }
    private void ApplyCourseAvailability(LessonAvailability state)
    {
        breakBlocked = App.Settings.RollCall.DisableDuringBreak && state.IsBreak;
        UpdateRollCallButtons();
    }
    private void UpdateRollCallButtons()
    {
        RollCallButton.IsEnabled = !drawBusy && !breakBlocked;
        RollCallButton.ToolTip = breakBlocked ? "当前为课间，已禁用点名；上课后自动恢复。" : "名单与权重在设置中调整";
        ToolTipService.SetShowOnDisabled(RollCallButton, true);
        floatingWindow?.SetAvailability(drawBusy, breakBlocked);
        resultWindow?.SetAvailability(drawBusy, breakBlocked);
    }
}
