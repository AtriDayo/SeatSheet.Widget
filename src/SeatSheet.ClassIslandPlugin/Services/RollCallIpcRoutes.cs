using dotnetCampus.Ipc.IpcRouteds.DirectRouteds;
using SeatSheet.ClassIslandPlugin.Reception;
using SeatSheet.RollCall.Protocol;

namespace SeatSheet.ClassIslandPlugin.Services;

public static class RollCallIpcRoutes
{
    public static void Register(JsonIpcDirectRoutedProvider provider, RollCallReceiver receiver)
    {
        provider.AddRequestHandler<RollCallHello>(RollCallProtocol.HelloRoute, receiver.Hello);
        provider.AddRequestHandler<RollCallMessage, RollCallReceipt>(RollCallProtocol.NotifyRoute, receiver.ReceiveAsync);
    }
}
