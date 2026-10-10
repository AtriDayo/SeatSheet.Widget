using dotnetCampus.Ipc.IpcRouteds.DirectRouteds;
using SeatSheet.ClassIslandPlugin.Reception;
using SeatSheet.RollCall.Protocol;

namespace SeatSheet.ClassIslandPlugin.Services;

public static class RollCallIpcRoutes
{
    public static void Register(JsonIpcDirectRoutedProvider provider, RollCallReceiver receiver, ClientConnectionState? connection = null)
    {
        provider.AddRequestHandler<RollCallHello>(RollCallProtocol.HelloRoute, () =>
        {
            connection?.RecordContact();
            return receiver.Hello();
        });
        provider.AddRequestHandler<RollCallMessage, RollCallReceipt>(RollCallProtocol.NotifyRoute, async message =>
        {
            var receipt = await receiver.ReceiveAsync(message);
            if (receipt.Status is "accepted" or "duplicate") connection?.RecordContact();
            return receipt;
        });
    }
}
