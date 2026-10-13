using System;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.Shared.Enums;
using ClassIsland.Shared.IPC;
using ClassIsland.Shared.IPC.Abstractions.Services;
using dotnetCampus.Ipc.CompilerServices.GeneratedProxies;
using dotnetCampus.Ipc.Pipes;

namespace SeatSheet.Widget;

public sealed record LessonAvailability(bool IsBreak, string Description)
{
    public static LessonAvailability Unknown { get; } = new(false, "课程状态不可用，允许点名。");
    public static LessonAvailability FromState(bool running, bool enabled, bool loaded, bool confirmed, TimeState before, TimeState after)
    {
        if (!running || !enabled || !loaded || !confirmed || before != after) return Unknown;
        return after == TimeState.Breaking ? new(true, "当前为课间，已禁用点名。")
            : new(false, "当前不是课间，允许点名。");
    }
}

public sealed class ClassIslandLessonClient(string? pipeName = null)
{
    private readonly string _pipeName = pipeName ?? IpcClient.PipeName;
    private readonly object _gate = new();
    private Task<LessonAvailability>? _pending;
    public Task<LessonAvailability> ReadAsync()
    {
        // Settings checks and draw actions share an in-flight read, never bypass a
        // known break merely because a second request arrived at the same time.
        lock (_gate) return _pending is { IsCompleted: false } ? _pending : _pending = ReadCoreAsync();
    }
    private async Task<LessonAvailability> ReadCoreAsync()
    {
        try
        {
            using var provider = new IpcProvider();
            provider.StartServer();
            using var deadline = new CancellationTokenSource(700);
            var connect = provider.GetAndConnectToPeerAsync(_pipeName);
            _ = connect.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            var peer = await connect.WaitAsync(deadline.Token);
            // Official proxy property getters are synchronous. A short-lived dedicated
            // worker keeps them off WPF and avoids starving IPC's response thread pool.
            var read = Task.Factory.StartNew(() =>
            {
                var lessons = provider.CreateIpcProxy<IPublicLessonsService>(peer, new IpcProxyConfigs { Timeout = 250, IgnoresIpcException = false });
                // Read-only properties from the official 2.1.0.1 contract, never class data.
                var before = lessons.CurrentState;
                var running = lessons.IsTimerRunning;
                var enabled = lessons.IsClassPlanEnabled;
                var loaded = lessons.IsClassPlanLoaded;
                var confirmed = lessons.IsLessonConfirmed;
                return LessonAvailability.FromState(running, enabled, loaded, confirmed, before, lessons.CurrentState);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try { return await read.WaitAsync(deadline.Token); }
            finally { _ = read.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted); }
        }
        catch { return LessonAvailability.Unknown; }
    }
}
