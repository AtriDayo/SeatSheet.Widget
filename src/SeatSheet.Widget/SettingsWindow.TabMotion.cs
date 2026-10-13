using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SeatSheet.Widget;

public partial class SettingsWindow
{
    private ContentPresenter? tabPresenter;
    private FrameworkElement? tabStage;
    private int shownTab = -1, requestedTab, tabMotionVersion;
    private bool tabTransitionRunning;
    private Task tabMotionTask = Task.CompletedTask;
    private readonly List<TabCardMotion> movingCards = new();

    private sealed class TabCardMotion(FrameworkElement element)
    {
        internal readonly FrameworkElement Element = element;
        internal readonly Transform OriginalTransform = element.RenderTransform;
        internal readonly CacheMode? OriginalCache = element.CacheMode;
        internal readonly double OriginalOpacity = element.Opacity;
        internal readonly TranslateTransform Offset = new();
    }
    private void InitializeTabMotion()
    {
        Loaded += (_, _) =>
        {
            SettingsTabs.ApplyTemplate();
            tabPresenter = SettingsTabs.Template.FindName("TabMotionContent", SettingsTabs) as ContentPresenter;
            tabStage = SettingsTabs.Template.FindName("TabMotionStage", SettingsTabs) as FrameworkElement;
            requestedTab = SettingsTabs.SelectedIndex;
            ShowSettingsTab(requestedTab);
        };
        Closing += (_, _) => StopTabMotion();
        Closed += (_, _) => StopTabMotion();
    }
    private void RequestSettingsTabTransition()
    {
        requestedTab = SettingsTabs.SelectedIndex;
        if (tabPresenter == null) return;
        if (!IsLoaded || closed || closingMotion || TabAnimationOption.IsChecked != true || !SystemParameters.ClientAreaAnimation)
        { StopTabMotion(); ShowSettingsTab(requestedTab); return; }
        if (tabTransitionRunning || shownTab == requestedTab) return;
        tabTransitionRunning = true;
        tabPresenter.IsHitTestVisible = false;
        tabMotionTask = AnimateSettingsTabsAsync(++tabMotionVersion);
    }
    private void ShowSettingsTab(int index)
    {
        if (tabPresenter == null || index < 0 || index >= SettingsTabs.Items.Count) return;
        ResetTabCards();
        tabPresenter.Content = ((TabItem)SettingsTabs.Items[index]).Content;
        shownTab = index;
        // One layout at the page boundary; animation frames only change render properties.
        tabPresenter.UpdateLayout();
    }
    private IEnumerable<FrameworkElement> VisibleTabCards()
    {
        if (tabPresenter == null || tabStage == null) yield break;
        var viewport = new Rect(0, 0, tabStage.ActualWidth, tabStage.ActualHeight);
        foreach (var element in FindCards(tabPresenter))
        {
            if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) continue;
            var bounds = element.TransformToAncestor(tabStage).TransformBounds(new Rect(element.RenderSize));
            if (bounds.IntersectsWith(viewport)) yield return element;
        }
    }
    private static IEnumerable<FrameworkElement> FindCards(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement { Tag: "SettingsMotionCard" } card) yield return card;
            else foreach (var nested in FindCards(child)) yield return nested;
        }
    }
    private int AnimateTabCards(bool entering, double exitDirection)
    {
        var cards = VisibleTabCards().ToList();
        for (var i = 0; i < cards.Count; i++)
        {
            var motion = new TabCardMotion(cards[i]); movingCards.Add(motion);
            var transform = new TransformGroup(); transform.Children.Add(motion.OriginalTransform); transform.Children.Add(motion.Offset);
            motion.Element.RenderTransform = transform;
            motion.Element.CacheMode = new BitmapCache();
            motion.Element.Opacity = entering ? 0 : motion.OriginalOpacity;
            motion.Offset.Y = entering ? -14 : 0;
            var delay = TimeSpan.FromMilliseconds(i * (entering ? 28 : 18));
            var duration = TimeSpan.FromMilliseconds(entering ? 210 : 115);
            var ease = new CubicEase { EasingMode = entering ? EasingMode.EaseOut : EasingMode.EaseIn };
            motion.Element.BeginAnimation(OpacityProperty, new DoubleAnimation(entering ? 0 : motion.OriginalOpacity, entering ? motion.OriginalOpacity : 0, duration) { BeginTime = delay, EasingFunction = ease });
            if (entering) motion.Offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-14, 0, duration) { BeginTime = delay, EasingFunction = ease });
            else motion.Offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, exitDirection * 24, duration) { BeginTime = delay, EasingFunction = ease });
        }
        return cards.Count == 0 ? 0 : (entering ? 210 : 115) + (cards.Count - 1) * (entering ? 28 : 18);
    }
    private async Task AnimateSettingsTabsAsync(int version)
    {
        try
        {
            while (requestedTab != shownTab && version == tabMotionVersion && !closed)
            {
                var direction = requestedTab > shownTab ? -1 : 1;
                await Task.Delay(AnimateTabCards(false, direction) + 16);
                if (version != tabMotionVersion || closed) return;
                // A burst of header clicks exits once and enters the last requested page.
                ShowSettingsTab(requestedTab);
                await Task.Delay(AnimateTabCards(true, direction) + 16);
                if (version != tabMotionVersion || closed) return;
                ResetTabCards();
            }
        }
        catch
        {
            // A rendering failure must not strand the settings page or its draft.
            if (version == tabMotionVersion && !closed) ShowSettingsTab(requestedTab);
        }
        finally
        {
            if (version == tabMotionVersion)
            {
                ResetTabCards(); tabTransitionRunning = false;
                if (tabPresenter != null) tabPresenter.IsHitTestVisible = true;
            }
        }
    }
    private void ResetTabCards()
    {
        foreach (var card in movingCards)
        {
            card.Element.BeginAnimation(OpacityProperty, null);
            card.Offset.BeginAnimation(TranslateTransform.XProperty, null);
            card.Offset.BeginAnimation(TranslateTransform.YProperty, null);
            card.Element.Opacity = card.OriginalOpacity;
            card.Element.RenderTransform = card.OriginalTransform;
            card.Element.CacheMode = card.OriginalCache;
        }
        movingCards.Clear();
    }
    private void StopTabMotion()
    {
        ++tabMotionVersion; ResetTabCards(); tabTransitionRunning = false;
        if (tabPresenter != null) tabPresenter.IsHitTestVisible = true;
    }
    private void TabAnimationDisabled(object sender, RoutedEventArgs e)
    {
        if (tabPresenter == null) return;
        StopTabMotion(); ShowSettingsTab(SettingsTabs.SelectedIndex);
    }
}
