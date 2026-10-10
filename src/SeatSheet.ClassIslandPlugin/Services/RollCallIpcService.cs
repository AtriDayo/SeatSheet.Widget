using ClassIsland.Core.Abstractions.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeatSheet.ClassIslandPlugin.Reception;

namespace SeatSheet.ClassIslandPlugin.Services;

public sealed class RollCallIpcService : IHostedService
{
    private readonly RollCallReceiver _receiver;
    private readonly ILogger<RollCallIpcService> _logger;

    public RollCallIpcService(IIpcService ipc, RollCallReceiver receiver, ILogger<RollCallIpcService> logger, ClientConnectionState connection)
    {
        _receiver = receiver;
        _logger = logger;
        RollCallIpcRoutes.Register(ipc.JsonRoutedProvider, receiver, connection);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try { await _receiver.InitializeAsync(); }
        catch { _logger.LogError("SeatSheet 点名去重记录无法读取，接收端已停用。请检查插件设置目录中的 dedup.json。"); }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _receiver.Stop();
        return Task.CompletedTask;
    }
}
