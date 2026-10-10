using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SeatSheet.Widget;

public partial class SettingsWindow
{
    private readonly RollCallOptions rollCallDraft = App.Settings.RollCall.Clone();
    private List<RollCallParticipant> participants = new();
    private CachedPlan? rosterPlan;
    private bool resetRound;
    private bool probingPlugin;
    private string? draftProbeStatus;
    private readonly DispatcherTimer pluginStatusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private void InitializeRollCall(CachedPlan? plan)
    {
        RollCallOutput.SelectedIndex = rollCallDraft.Output == "both" ? 2 : rollCallDraft.Output == "classIsland" ? 1 : 0;
        NoRepeatOption.IsChecked = rollCallDraft.NoRepeat;
        LoadRoster(plan);
        pluginStatusTimer.Tick += (_, _) => UpdatePluginStatus();
        Loaded += (_, _) => { UpdatePluginStatus(); pluginStatusTimer.Start(); };
        Closed += (_, _) => pluginStatusTimer.Stop();
    }
    private void OutputSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        draftProbeStatus = null;
        UpdatePluginStatus();
    }
    private void UpdatePluginStatus()
    {
        if (PluginConnection == null || ProbePluginButton == null) return;
        var external = RollCallOutput.SelectedIndex > 0;
        PluginConnection.Text = !external ? "本软件显示，无需连接 ClassIsland"
            : App.Settings.RollCall.Output == "widget" ? draftProbeStatus ?? "尚未检查 ClassIsland 插件"
            : App.Notifications.Status;
        if (probingPlugin) PluginConnection.Text = "正在检查 ClassIsland 插件…";
        ProbePluginButton.IsEnabled = external && !probingPlugin;
        ProbePluginButton.ToolTip = external ? "检查 ClassIsland 插件是否可以接收通知" : "选择 ClassIsland 显示或两者同时后可检查插件";
    }
    private void SettingsTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != SettingsTabs || SettingsDescription == null) return;
        SettingsDescription.Text = SettingsTabs.SelectedIndex == 1
            ? "管理参与名单、抽取权重和点名结果的显示位置。"
            : "连接班级座位表，调整适合课堂使用的显示大小。";
    }
    private void LoadRoster(CachedPlan? plan)
    {
        rosterPlan = plan;
        participants = plan == null ? new() : RollCallRoster.Create(plan.Source, plan.Plan, rollCallDraft);
        ParticipantGrid.ItemsSource = participants;
        RosterSummary.Text = plan == null ? "尚未加载座位表，请打开座位表或更新名单。" : $"{plan.Plan.Name} · {participants.Count} 人 · 不包含空座";
    }
    private void ApplyRosterDraft()
    {
        if (!ParticipantGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !ParticipantGrid.CommitEdit(DataGridEditingUnit.Row, true) ||
            HasValidationError(ParticipantGrid)) throw new ArgumentException("请修正名单中的权重：需为 0–100 的整数。");
        if (rosterPlan != null) RollCallRoster.SaveRules(rosterPlan.Source, rosterPlan.Plan, participants, rollCallDraft);
        rollCallDraft.Output = RollCallOutput.SelectedIndex == 2 ? "both" : RollCallOutput.SelectedIndex == 1 ? "classIsland" : "widget";
        rollCallDraft.NoRepeat = NoRepeatOption.IsChecked == true;
        rollCallDraft.Validate();
    }
    private static bool HasValidationError(DependencyObject element)
    {
        if (Validation.GetHasError(element)) return true;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
            if (HasValidationError(System.Windows.Media.VisualTreeHelper.GetChild(element, i))) return true;
        return false;
    }
    private async void RefreshRosterClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender; button.IsEnabled = false;
        try
        {
            ApplyRosterDraft();
            var plan = await SeatApi.Fetch(App.Settings.ServerUrl);
            if (closed) return;
            LoadRoster(plan);
            RollCallSettingsMessage.Text = "名单已更新，权重和缺席设置保留。点击保存设置后生效。";
        }
        catch { RollCallSettingsMessage.Text = "更新失败，请检查网络或权重输入；当前名单和设置保留。"; }
        finally { button.IsEnabled = true; }
    }
    private async void ProbePluginClick(object sender, RoutedEventArgs e)
    {
        if (probingPlugin) return;
        probingPlugin = true; UpdatePluginStatus();
        try
        {
            var delivery = await App.Notifications.ProbeAsync();
            if (!closed) draftProbeStatus = $"最近检查：{delivery.Description}（{DateTime.Now:HH:mm:ss}）";
        }
        finally { probingPlugin = false; UpdatePluginStatus(); }
    }
    private void ResetRoundClick(object sender, RoutedEventArgs e)
    {
        resetRound = true;
        RollCallSettingsMessage.Text = "点击保存设置后重置本轮；取消不会重置。";
    }
    internal async Task VerifyRollCallDraftForSmoke(bool save)
    {
        if (!App.IsSmoke) throw new InvalidOperationException("Requires isolated QA.");
        var before = System.Text.Json.JsonSerializer.Serialize(App.Settings.RollCall);
        participants[0].Weight = 3;
        participants[1].Absent = true;
        NoRepeatOption.IsChecked = true;
        if (save)
        {
            var saved = false;
            Saved += (_, _) => saved = true;
            await CheckConnection(true);
            if (!saved || !App.Settings.RollCall.NoRepeat ||
                App.Settings.RollCall.Classes[RollCallRoster.Scope(rosterPlan!.Source, rosterPlan.Plan)][participants[0].Id].Weight != 3)
                throw new InvalidOperationException("Roll-call draft did not save.");
        }
        else
        {
            Close();
            if (before != System.Text.Json.JsonSerializer.Serialize(App.Settings.RollCall)) throw new InvalidOperationException("Cancel mutated live roll-call settings.");
        }
    }
}
