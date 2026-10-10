using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using SeatSheet.ClassIslandPlugin.Reception;
using SeatSheet.ClassIslandPlugin.Presentation;
using SeatSheet.RollCall.Protocol;

namespace SeatSheet.ClassIslandPlugin.Services;

[NotificationProviderInfo("21fef20b-ff67-49da-8cbd-f10e8f5a07b9", "SeatSheet 点名", "展示已抽取的点名结果，音效和朗读遵循 ClassIsland 设置。")]
public sealed class RollCallNotificationProvider(NotificationDisplaySettingsStore displaySettings) : NotificationProviderBase, IRollCallNotificationSink
{
    public async Task SubmitAsync(RollCallMessage message)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (message.ExpiresAtUtc <= DateTimeOffset.UtcNow) throw new TimeoutException("Reminder expired before submission.");
            var student = message.Student!;
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(student.ClassName)) details.Add(student.ClassName);
            if (student.Seat is { } seat)
                details.Add(!string.IsNullOrWhiteSpace(seat.Label) ? seat.Label : $"第{seat.Row}排第{seat.Column}列");
            var timing = NotificationTiming.Create(displaySettings.DurationSeconds, details.Count > 0);
            var mask = NotificationContent.CreateSimpleTextContent(student.Name!, content =>
            {
                content.Duration = TimeSpan.FromSeconds(timing.MaskSeconds);
                content.SpeechContent = student.Name!;
            });
            var overlay = details.Count == 0 ? null : NotificationContent.CreateSimpleTextContent(
                $"{student.Name} · {string.Join(" · ", details)}", content =>
                {
                    content.Duration = TimeSpan.FromSeconds(timing.OverlaySeconds);
                    content.IsSpeechEnabled = false;
                });
            // Snapshot duration at submission; slider changes do not change an active reminder.
            // The synchronous call runs on the UI thread and does not wait for the reminder to finish.
            // Do not override RequestNotificationSettings: retain host/provider sound, speech and effects.
            ShowNotification(new NotificationRequest { MaskContent = mask, OverlayContent = overlay });
        });
    }
}
