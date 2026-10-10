using System;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.Shared.IPC;
using dotnetCampus.Ipc.IpcRouteds.DirectRouteds;
using dotnetCampus.Ipc.Pipes;
using SeatSheet.RollCall.Protocol;

namespace SeatSheet.Widget;

public sealed record NotificationDelivery(bool Success, string Description);
public sealed class ClassIslandRollCallClient(string? pipeName = null) : IDisposable
{
    private readonly string _pipeName = pipeName ?? IpcClient.PipeName;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _status = "尚未连接";
    public string Status => Volatile.Read(ref _status);
    public void Start(Func<bool> enabled) => _ = MonitorAsync(enabled);
    private async Task MonitorAsync(Func<bool> enabled)
    {
        while (!_shutdown.IsCancellationRequested)
        {
            if (enabled()) await ProbeAsync();
            else Volatile.Write(ref _status, "本软件显示，无需连接");
            try { await Task.Delay(TimeSpan.FromSeconds(10), _shutdown.Token); }
            catch (OperationCanceledException) { break; }
        }
    }
    public Task<NotificationDelivery> ProbeAsync() => RequestAsync(null);
    public Task<NotificationDelivery> SendAsync(RollCallMessage message) => RequestAsync(message);
    private async Task<NotificationDelivery> RequestAsync(RollCallMessage? message)
    {
        await _gate.WaitAsync();
        try
        {
            if (message != null && message.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                return new(false, "通知已过期，原抽取结果保留。请勿重新抽取来重试通知。");
            if (message != null && MessageValidator.Validate(message, DateTimeOffset.UtcNow) != null)
                return new(false, "姓名或班级信息不符合插件通知格式，已保留本地抽取结果。");
            using var provider = new IpcProvider();
            var routes = new JsonIpcDirectRoutedProvider(provider);
            routes.StartServer();
            var deadline = Task.Delay(TimeSpan.FromSeconds(3), _shutdown.Token);
            async Task<T> Bounded<T>(Task<T> operation)
            {
                if (await Task.WhenAny(operation, deadline) != operation)
                {
                    _ = operation.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    throw new TimeoutException();
                }
                return await operation;
            }
            var peer = await Bounded(routes.GetAndConnectClientAsync(_pipeName));
            var hello = await Bounded(peer.GetResponseAsync<RollCallHello>(RollCallProtocol.HelloRoute));
            if (hello is not { Ready: true, ProtocolVersion: RollCallProtocol.Version })
            {
                Volatile.Write(ref _status, "插件未就绪或协议不兼容");
                return new(false, Status);
            }
            Volatile.Write(ref _status, "已连接 SeatSheet 插件");
            if (message == null) return new(true, Status);
            var receipt = await Bounded(peer.GetResponseAsync<RollCallReceipt>(RollCallProtocol.NotifyRoute, message));
            if (receipt == null || receipt.MessageId != message.MessageId || receipt.ReceiverInstanceId != hello.ReceiverInstanceId)
                return new(false, "通知回执不匹配，原抽取结果保留。");
            return receipt.Status switch
            {
                "accepted" => new(true, "已交给 ClassIsland；展示效果遵循提醒设置。"),
                "duplicate" => new(true, "插件已记录此结果，不会重复提醒；此前提醒可能未完成。"),
                "expired" => new(false, "通知已过期，原抽取结果保留。"),
                "busy" => new(false, "插件忙碌，可重发原结果。"),
                _ => new(false, "插件拒绝或未能处理通知，原抽取结果保留。")
            };
        }
        catch
        {
            Volatile.Write(ref _status, "未连接：ClassIsland 或插件不可用");
            return new(false, "通知发送失败或回执超时，结果已保留；可重发同一结果。");
        }
        finally { _gate.Release(); }
    }
    public void Dispose() => _shutdown.Cancel();
}
