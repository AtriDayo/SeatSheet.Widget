using ClassIsland.Shared.Enums;
using ClassIsland.Shared.IPC.Abstractions.Services;
using ClassIsland.Shared.Models.Profile;

internal sealed class TestLessons : IPublicLessonsService
{
    public bool IsTimerRunning { get; set; } = true;
    public bool IsClassPlanEnabled { get; set; } = true;
    public bool IsClassPlanLoaded { get; set; } = true;
    public bool IsLessonConfirmed { get; set; } = true;
    public TimeState CurrentState { get; set; } = TimeState.Breaking;
    public ClassPlan? CurrentClassPlan { get; set; }
    public int CurrentSelectedIndex { get; set; }
    public Subject NextClassSubject { get; set; } = Subject.Fallback;
    public Subject? CurrentSubject { get; set; }
    public TimeLayoutItem NextBreakingTimeLayoutItem { get; set; } = TimeLayoutItem.Empty;
    public TimeLayoutItem NextClassTimeLayoutItem { get; set; } = TimeLayoutItem.Empty;
    public TimeLayoutItem CurrentTimeLayoutItem { get; set; } = TimeLayoutItem.Empty;
    public TimeSpan OnClassLeftTime { get; set; }
    public TimeSpan OnBreakingTimeLeftTime { get; set; }
    public ClassPlan? GetClassPlanByDate(DateTime date) => null;
}
