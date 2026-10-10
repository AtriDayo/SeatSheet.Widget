using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SeatSheet.ClassIslandPlugin.Reception;
using SeatSheet.ClassIslandPlugin.Presentation;
using SeatSheet.ClassIslandPlugin.Services;
using SeatSheet.ClassIslandPlugin.Views;

namespace SeatSheet.ClassIslandPlugin;

[PluginEntrance]
public sealed class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton(new NotificationDisplaySettingsStore(Path.Combine(PluginConfigFolder, "notification.json")));
        services.AddNotificationProvider<RollCallNotificationProvider>();
        // Share the registered provider with the receiver, rather than construct a second provider.
        var hostedProvider = services.Single(x => x.ServiceType == typeof(IHostedService) &&
                                                 x.ImplementationType == typeof(RollCallNotificationProvider));
        services.Remove(hostedProvider);
        services.AddSingleton<RollCallNotificationProvider>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RollCallNotificationProvider>());
        services.AddSingleton<IRollCallNotificationSink>(sp => sp.GetRequiredService<RollCallNotificationProvider>());
        services.AddSingleton<IDedupStore>(new FileDedupStore(Path.Combine(PluginConfigFolder, "dedup.json")));
        services.AddSingleton<RollCallReceiver>();
        services.AddHostedService<RollCallIpcService>();
        services.AddSettingsPage<RollCallSettingsPage>();
    }
}
