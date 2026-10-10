using dotnetCampus.Ipc.IpcRouteds.DirectRouteds;
using dotnetCampus.Ipc.Pipes;
using Newtonsoft.Json;
using SeatSheet.ClassIslandPlugin.Reception;
using SeatSheet.ClassIslandPlugin.Services;
using SeatSheet.ClassIslandPlugin.Presentation;
using SeatSheet.RollCall.Protocol;

var checks = 0;
void Check(bool value, string label)
{
    if (!value) throw new Exception(label);
    Console.WriteLine("PASS " + label);
    checks++;
}
RollCallMessage Message(DateTimeOffset now) => new()
{
    MessageId = Guid.NewGuid().ToString("D"), CreatedAtUtc = now, ExpiresAtUtc = now.AddSeconds(30),
    Student = new() { Name = "示例同学", ClassName = "示例班级", Seat = new() { Row = 2, Column = 3 } }
};
var clock = new TestClock(DateTimeOffset.UtcNow);
var message = Message(clock.GetUtcNow());
Check(MessageValidator.Validate(message, clock.GetUtcNow()) is null, "Valid Chinese result and one-based seat");
Check(JsonConvert.SerializeObject(message).Contains("\"messageId\""), "Official IPC serializer uses camel-case protocol fields");
Check(JsonConvert.DeserializeObject<RollCallMessage>(System.Text.Json.JsonSerializer.Serialize(message)) == message,
    "System.Text.Json and official IPC JSON serializer agree");
Check(MessageValidator.Validate(null, clock.GetUtcNow()) == "missingMessage", "Reject null message");
Check(MessageValidator.Validate(message with { ProtocolVersion = 9 }, clock.GetUtcNow()) == "unsupportedVersion", "Reject unsupported version");
Check(MessageValidator.Validate(message with { MessageId = Guid.Empty.ToString("D") }, clock.GetUtcNow()) == "invalidMessageId", "Reject empty ID");
Check(MessageValidator.Validate(message with { Type = "drawAgain" }, clock.GetUtcNow()) == "invalidType", "Receiver never accepts draw commands");
Check(MessageValidator.Validate(message with { Student = new() { Name = " " } }, clock.GetUtcNow()) == "invalidName", "Reject blank name");
Check(MessageValidator.Validate(message with { Student = new() { Name = new string('x', 65) } }, clock.GetUtcNow()) == "invalidName", "Bound name length");
Check(MessageValidator.Validate(message with { Student = new() { Name = "示例\n同学" } }, clock.GetUtcNow()) == "invalidName", "Reject control characters");
Check(MessageValidator.Validate(message with { Student = message.Student! with { ClassName = new string('x', 129) } }, clock.GetUtcNow()) == "invalidClassName", "Bound class text");
Check(MessageValidator.Validate(message with { Student = message.Student! with { Seat = new() { Row = 0, Column = 2 } } }, clock.GetUtcNow()) == "invalidSeat", "Reject zero-based seat");
Check(MessageValidator.Validate(message with { Student = message.Student! with { Seat = new() { Row = 2 } } }, clock.GetUtcNow()) == "invalidSeat", "Reject incomplete coordinates");
Check(MessageValidator.Validate(message with { Student = new() { Name = "示例同学", Seat = new() { Label = "示例座位" } } }, clock.GetUtcNow()) is null, "Accept label-only seat");
Check(MessageValidator.Validate(message with { Student = new() { Name = "示例同学" } }, clock.GetUtcNow()) is null, "Class and seat are optional");
Check(MessageValidator.Validate(message with { ExpiresAtUtc = clock.GetUtcNow() }, clock.GetUtcNow()) == "invalidTime", "Reject zero lifetime");
Check(MessageValidator.Validate(message with { ExpiresAtUtc = clock.GetUtcNow().AddSeconds(61) }, clock.GetUtcNow()) == "invalidTime", "Bound message lifetime");
Check(MessageValidator.Validate(message with { CreatedAtUtc = clock.GetUtcNow().AddSeconds(6) }, clock.GetUtcNow()) == "invalidTime", "Reject future messages beyond clock tolerance");
Check(MessageValidator.Validate(message with { CreatedAtUtc = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(8)) }, clock.GetUtcNow()) == "invalidTime", "Require UTC timestamps");

var store = new MemoryStore();
var sink = new TestSink { Block = new(TaskCreationOptions.RunContinuationsAsynchronously) };
var receiver = new RollCallReceiver(sink, store, clock);
Check(!receiver.Hello().Ready, "Receiver is unavailable until dedup state is loaded");
await receiver.InitializeAsync();
Check(receiver.Hello().Ready, "Hello reports readiness");
var concurrentRequests = Enumerable.Range(0, 8).Select(_ => receiver.ReceiveAsync(message)).ToArray();
Check(store.Entries.Any(x => x.MessageId.ToString("D") == message.MessageId) && sink.Calls == 1,
    "Persist reservation before the native reminder is submitted");
sink.Block.SetResult();
var concurrent = await Task.WhenAll(concurrentRequests);
Check(concurrent.Count(x => x.Status == "accepted") == 1 && concurrent.Count(x => x.Status == "duplicate") == 7 && sink.Calls == 1,
    "Concurrent retries produce exactly one submission");
Check((await receiver.ReceiveAsync(message with { MessageId = message.MessageId!.ToUpperInvariant() })).Status == "duplicate", "Canonicalize ID casing");
Check((await receiver.ReceiveAsync(message with { Student = new() { Name = "另一示例" } })).Code == "messageIdConflict", "Reject same ID with changed result");
var restarted = new RollCallReceiver(sink, store, clock);
await restarted.InitializeAsync();
Check(restarted.InstanceId != receiver.InstanceId && (await restarted.ReceiveAsync(message)).Status == "duplicate" && sink.Calls == 1,
    "Restart retains deduplication with a new receiver instance");
Check((await restarted.ReceiveAsync(Message(clock.GetUtcNow()))).Status == "accepted" && sink.Calls == 2, "New draw ID can remind the same name");
clock.Now = clock.Now.AddSeconds(31);
Check((await receiver.ReceiveAsync(message)).Status == "expired" && sink.Calls == 2, "Expired retry does not replay");

var failingStore = new MemoryStore { FailSave = true };
var untouchedSink = new TestSink();
var storageReceiver = new RollCallReceiver(untouchedSink, failingStore, clock);
await storageReceiver.InitializeAsync();
Check((await storageReceiver.ReceiveAsync(Message(clock.GetUtcNow()))).Code == "dedupStorageFailed" && untouchedSink.Calls == 0 && !storageReceiver.Ready,
    "Disk failure never submits an unrecorded reminder and fails closed");
var failingSink = new TestSink { Fail = true };
var uncertain = new RollCallReceiver(failingSink, new MemoryStore(), clock);
await uncertain.InitializeAsync();
var uncertainMessage = Message(clock.GetUtcNow());
Check((await uncertain.ReceiveAsync(uncertainMessage)).Code == "notificationSubmissionUncertain", "Submission exception is a delivery failure");
Check((await uncertain.ReceiveAsync(uncertainMessage)).Status == "duplicate" && failingSink.Calls == 1,
    "Uncertain submission is not replayed by a retry");
receiver.Stop();
Check((await receiver.ReceiveAsync(Message(clock.GetUtcNow()))).Code == "receiverNotReady", "Stopped receiver refuses new results");

var blockedSink = new TestSink { Block = new(TaskCreationOptions.RunContinuationsAsynchronously) };
var saturated = new RollCallReceiver(blockedSink, new MemoryStore(), clock);
await saturated.InitializeAsync();
var pending = Enumerable.Range(0, 8).Select(_ => saturated.ReceiveAsync(Message(clock.GetUtcNow()))).ToArray();
Check((await saturated.ReceiveAsync(Message(clock.GetUtcNow()))).Status == "busy", "Bound in-flight requests");
clock.Now = clock.Now.AddSeconds(31);
blockedSink.Block.SetResult();
await Task.WhenAll(pending);
Check(blockedSink.Calls == 1 && pending.Skip(1).All(x => x.Result.Status == "expired"), "Queued results expire before submission");

var fullStore = new MemoryStore
{
    Entries = Enumerable.Range(0, RollCallProtocol.MaxDedupEntries)
        .Select(_ => new DedupEntry(Guid.NewGuid(), new string('A', 64), clock.GetUtcNow().AddSeconds(1))).ToArray()
};
var fullReceiver = new RollCallReceiver(new TestSink(), fullStore, clock);
await fullReceiver.InitializeAsync();
Check((await fullReceiver.ReceiveAsync(Message(clock.GetUtcNow()))).Code == "dedupFull", "Do not evict live dedup records to accept a new message");
clock.Now = clock.Now.AddSeconds(2);
Check((await fullReceiver.ReceiveAsync(Message(clock.GetUtcNow()))).Status == "accepted", "Reclaim expired records without polling");

var temporaryDirectory = Path.Combine(Path.GetTempPath(), "SeatSheet.Plugin.Checks." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporaryDirectory);
try
{
    var displayPath = Path.Combine(temporaryDirectory, "notification.json");
    var displaySettings = new NotificationDisplaySettingsStore(displayPath);
    Check(displaySettings.DurationSeconds == 5 && !displaySettings.LoadFailed, "Fresh install uses a 5-second reminder");
    await displaySettings.SaveDurationAsync(12);
    Check(new NotificationDisplaySettingsStore(displayPath).DurationSeconds == 12, "Display duration survives restart");
    var minimumTiming = NotificationTiming.Create(2, hasDetails: true);
    Check(minimumTiming.MaskSeconds > 0 && minimumTiming.OverlaySeconds > 0 && minimumTiming.MaskSeconds + minimumTiming.OverlaySeconds == 2,
        "Shortest reminder still shows both name and details within the selected total");
    var longTiming = NotificationTiming.Create(30, hasDetails: true);
    Check(longTiming.MaskSeconds == 2 && longTiming.MaskSeconds + longTiming.OverlaySeconds == 30,
        "Longer reminders extend the result body without extending name emphasis");
    Check(NotificationTiming.Create(12, hasDetails: false) == new NotificationTiming(12, 0), "Name-only result uses the full selected duration");
    var invalidDurationRejected = false;
    try { await displaySettings.SaveDurationAsync(31); } catch (ArgumentOutOfRangeException) { invalidDurationRejected = true; }
    Check(invalidDurationRejected && new NotificationDisplaySettingsStore(displayPath).DurationSeconds == 12,
        "Invalid duration cannot overwrite the saved setting");
    await File.WriteAllTextAsync(displayPath, "{\"DurationSeconds\":-1}");
    var recoveredDisplaySettings = new NotificationDisplaySettingsStore(displayPath);
    Check(recoveredDisplaySettings.LoadFailed && recoveredDisplaySettings.DurationSeconds == 5, "Invalid saved duration falls back to a safe default");
    await recoveredDisplaySettings.SaveDurationAsync(8);
    Check(!recoveredDisplaySettings.LoadFailed && new NotificationDisplaySettingsStore(displayPath).DurationSeconds == 8,
        "Changing duration repairs invalid settings");
    var blockedDisplayPath = Path.Combine(temporaryDirectory, "blocked");
    Directory.CreateDirectory(blockedDisplayPath);
    var unsavedDisplaySettings = new NotificationDisplaySettingsStore(blockedDisplayPath);
    var displaySaveFailed = false;
    try { await unsavedDisplaySettings.SaveDurationAsync(10); } catch (IOException) { displaySaveFailed = true; }
    catch (UnauthorizedAccessException) { displaySaveFailed = true; }
    Check(displaySaveFailed && unsavedDisplaySettings.DurationSeconds == 5, "Failed write retains the previous effective duration");
    var path = Path.Combine(temporaryDirectory, "dedup.json");
    var diskReceiver = new RollCallReceiver(new TestSink(), new FileDedupStore(path), clock);
    await diskReceiver.InitializeAsync();
    var diskMessage = Message(clock.GetUtcNow());
    Check((await diskReceiver.ReceiveAsync(diskMessage)).Status == "accepted", "Atomic file journal can be written");
    var journalText = await File.ReadAllTextAsync(path);
    Check(!journalText.Contains("示例") && !journalText.Contains("student", StringComparison.OrdinalIgnoreCase), "Journal contains no name, class or seat");
    var diskRestart = new RollCallReceiver(new TestSink(), new FileDedupStore(path), clock);
    await diskRestart.InitializeAsync();
    Check((await diskRestart.ReceiveAsync(diskMessage)).Status == "duplicate", "File journal survives a receiver restart");
    await File.WriteAllTextAsync(path, "broken");
    var corrupt = new RollCallReceiver(new TestSink(), new FileDedupStore(path), clock);
    var rejected = false;
    try { await corrupt.InitializeAsync(); } catch (System.Text.Json.JsonException) { rejected = true; }
    Check(rejected && !corrupt.Ready, "Corrupt journal fails closed without overwriting it");
}
finally
{
    var cleanupTarget = Path.GetFullPath(temporaryDirectory);
    var cleanupParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!cleanupTarget.StartsWith(cleanupParent, StringComparison.OrdinalIgnoreCase) ||
        !Path.GetFileName(cleanupTarget).StartsWith("SeatSheet.Plugin.Checks.", StringComparison.Ordinal))
        throw new InvalidOperationException("Unexpected cleanup path.");
    Directory.Delete(cleanupTarget, recursive: true);
}

// Test the actual official transport on private random pipe names. No ClassIsland instance is touched.
var serverName = "SeatSheet.Checks." + Guid.NewGuid().ToString("N");
using var server = new IpcProvider(serverName);
using var client = new IpcProvider();
var routes = new JsonIpcDirectRoutedProvider(server);
var clientRoutes = new JsonIpcDirectRoutedProvider(client);
var ipcSink = new TestSink();
var ipcReceiver = new RollCallReceiver(ipcSink, new MemoryStore());
var connectionClock = new ConnectionClock();
var connection = new ClientConnectionState(connectionClock);
Check(!connection.Connected, "Receiver initialization alone cannot mark a client connected");
await ipcReceiver.InitializeAsync();
RollCallIpcRoutes.Register(routes, ipcReceiver, connection);
routes.StartServer();
clientRoutes.StartServer();
var proxy = await clientRoutes.GetAndConnectClientAsync(serverName).WaitAsync(TimeSpan.FromSeconds(5));
var hello = await proxy.GetResponseAsync<RollCallHello>(RollCallProtocol.HelloRoute).WaitAsync(TimeSpan.FromSeconds(5));
Check(hello is { Ready: true, ProtocolVersion: 1 } && hello.ReceiverInstanceId == ipcReceiver.InstanceId, "Official named pipe hello round-trip");
Check(connection.Connected, "IPC handshake marks client activity");
connectionClock.Elapsed = TimeSpan.FromSeconds(30);
Check(!connection.Connected, "Client activity expires after 30 seconds without a heartbeat");
await ipcReceiver.ReceiveAsync(Message(DateTimeOffset.UtcNow));
Check(!connection.Connected, "Internal sample notifications never mark a client connected");
ipcSink.Calls = 0;
var ipcMessage = Message(DateTimeOffset.UtcNow);
var ack = await proxy.GetResponseAsync<RollCallReceipt>(RollCallProtocol.NotifyRoute, ipcMessage).WaitAsync(TimeSpan.FromSeconds(5));
Check(ack is { Status: "accepted" } && ack.MessageId == ipcMessage.MessageId && ipcSink.Calls == 1, "Official JSON result and receipt round-trip");
Check(connection.Connected, "Valid IPC result renews client activity");
var repeated = await proxy.GetResponseAsync<RollCallReceipt>(RollCallProtocol.NotifyRoute, ipcMessage).WaitAsync(TimeSpan.FromSeconds(5));
Check(repeated is { Status: "duplicate" } && ipcSink.Calls == 1, "Transport retry cannot produce a second notification");
connectionClock.Elapsed = TimeSpan.FromSeconds(60);
var invalidAck = await proxy.GetResponseAsync<RollCallReceipt>(RollCallProtocol.NotifyRoute, ipcMessage with { ProtocolVersion = 2 }).WaitAsync(TimeSpan.FromSeconds(5));
Check(invalidAck is { Status: "unsupportedVersion" }, "Protocol mismatch returns a business rejection");
Check(!connection.Connected, "Rejected messages do not renew client activity");
Console.WriteLine($"{checks} plugin checks passed.");

using var desktopClient = new SeatSheet.Widget.ClassIslandRollCallClient(serverName);
Check((await desktopClient.ProbeAsync()).Success, "Desktop client uses the official handshake route");
var desktopMessage = Message(DateTimeOffset.UtcNow);
var desktopBefore = ipcSink.Calls;
Check((await desktopClient.SendAsync(desktopMessage)).Success && ipcSink.Calls == desktopBefore + 1,
    "Desktop client sends a fixed result to the actual receiver");
Check((await desktopClient.SendAsync(desktopMessage)).Success && ipcSink.Calls == desktopBefore + 1,
    "Desktop resend retains the same ID and does not replay the notification");
Check(!(await desktopClient.SendAsync(desktopMessage with { Student = new() { Name = new string('示',65) } })).Success && ipcSink.Calls == desktopBefore + 1,
    "Notification format rejection cannot change or submit the original draw");
var expiredDesktopMessage = desktopMessage with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1) };
Check(!(await desktopClient.SendAsync(expiredDesktopMessage)).Success && ipcSink.Calls == desktopBefore + 1,
    "Expired desktop retry preserves the result and does not send a fresh draw");
using var unavailableClient = new SeatSheet.Widget.ClassIslandRollCallClient("SeatSheet.Missing." + Guid.NewGuid().ToString("N"));
var originalDesktopId = desktopMessage.MessageId;
var offlineDelivery = await unavailableClient.SendAsync(desktopMessage).WaitAsync(TimeSpan.FromSeconds(8));
Check(!offlineDelivery.Success && desktopMessage.MessageId == originalDesktopId,
    "Unavailable host returns a bounded notification failure");
Console.WriteLine($"{checks} total plugin/client checks passed.");

sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
sealed class MemoryStore : IDedupStore
{
    public IReadOnlyList<DedupEntry> Entries { get; set; } = [];
    public bool FailSave { get; init; }
    public Task<IReadOnlyList<DedupEntry>> LoadAsync() => Task.FromResult(Entries);
    public Task SaveAsync(IReadOnlyCollection<DedupEntry> entries)
    {
        if (FailSave) throw new IOException("Simulated storage failure.");
        Entries = entries.ToArray();
        return Task.CompletedTask;
    }
}
sealed class TestSink : IRollCallNotificationSink
{
    public int Calls { get; set; }
    public bool Fail { get; init; }
    public TaskCompletionSource? Block { get; init; }
    public async Task SubmitAsync(RollCallMessage message)
    {
        Calls++;
        if (Fail) throw new InvalidOperationException("Simulated submission failure.");
        if (Block is not null) await Block.Task;
    }
}

sealed class ConnectionClock : TimeProvider
{
    public TimeSpan Elapsed { get; set; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Elapsed.Ticks;
}
