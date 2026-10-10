using SeatSheet.RollCall.Protocol;

namespace SeatSheet.ClassIslandPlugin.Reception;

public sealed class RollCallReceiver(IRollCallNotificationSink sink, IDedupStore store, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, DedupEntry> _entries = [];
    private int _pending;
    private volatile bool _ready;
    public string InstanceId { get; } = Guid.NewGuid().ToString("D");
    public bool Ready => _ready;
    public RollCallHello Hello() => new() { ReceiverInstanceId = InstanceId, Ready = Ready };

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _ready = false;
            var entries = await store.LoadAsync();
            if (entries.Count > RollCallProtocol.MaxDedupEntries) throw new InvalidDataException("Too many dedup entries.");
            var now = _time.GetUtcNow();
            var loaded = new Dictionary<Guid, DedupEntry>();
            foreach (var entry in entries)
            {
                if (entry.MessageId == Guid.Empty || entry.Fingerprint is null || entry.Fingerprint.Length != 64 ||
                    !entry.Fingerprint.All(Uri.IsHexDigit) || entry.RetainUntilUtc.Offset != TimeSpan.Zero ||
                    entry.RetainUntilUtc > now + RollCallProtocol.MaxLifetime + RollCallProtocol.ClockTolerance + RollCallProtocol.DedupGracePeriod ||
                    !loaded.TryAdd(entry.MessageId, entry)) throw new InvalidDataException("Invalid dedup entry.");
            }
            _entries.Clear();
            foreach (var entry in loaded.Values.Where(x => x.RetainUntilUtc > now)) _entries.Add(entry.MessageId, entry);
            _ready = true;
        }
        finally { _gate.Release(); }
    }

    public void Stop() => _ready = false;

    public async Task<RollCallReceipt> ReceiveAsync(RollCallMessage? message)
    {
        var error = MessageValidator.Validate(message, _time.GetUtcNow());
        if (error is not null) return Receipt(message, error is "expired" or "unsupportedVersion" ? error : "invalid", error);
        if (!Ready) return Receipt(message, "internalError", "receiverNotReady");
        if (Interlocked.Increment(ref _pending) > RollCallProtocol.MaxPendingRequests)
        {
            Interlocked.Decrement(ref _pending);
            return Receipt(message, "busy", "queueFull");
        }
        await _gate.WaitAsync();
        try
        {
            if (!Ready) return Receipt(message, "internalError", "receiverNotReady");
            var now = _time.GetUtcNow();
            if (message!.ExpiresAtUtc <= now) return Receipt(message, "expired", "expired");
            var id = Guid.Parse(message.MessageId!);
            var fingerprint = MessageValidator.Fingerprint(message);
            foreach (var key in _entries.Where(x => x.Value.RetainUntilUtc <= now).Select(x => x.Key).ToArray()) _entries.Remove(key);
            if (_entries.TryGetValue(id, out var existing))
                return Receipt(message, existing.Fingerprint == fingerprint ? "duplicate" : "invalid",
                    existing.Fingerprint == fingerprint ? "alreadyReserved" : "messageIdConflict");
            if (_entries.Count >= RollCallProtocol.MaxDedupEntries) return Receipt(message, "busy", "dedupFull");

            // Reserve durably BEFORE submission. A crash can lose a reminder, but retries cannot replay it.
            _entries.Add(id, new(id, fingerprint, message.ExpiresAtUtc + RollCallProtocol.DedupGracePeriod));
            try { await store.SaveAsync(_entries.Values.ToArray()); }
            catch
            {
                _ready = false; // Uncertain storage state: fail closed instead of silently losing deduplication.
                return Receipt(message, "internalError", "dedupStorageFailed");
            }
            if (!Ready) return Receipt(message, "internalError", "receiverStoppedBeforeSubmission");
            if (message.ExpiresAtUtc <= _time.GetUtcNow()) return Receipt(message, "expired", "expiredBeforeSubmission");
            try { await sink.SubmitAsync(message); }
            catch { return Receipt(message, "internalError", "notificationSubmissionUncertain"); }
            return Receipt(message, "accepted", "submittedToClassIsland");
        }
        finally
        {
            _gate.Release();
            Interlocked.Decrement(ref _pending);
        }
    }

    private RollCallReceipt Receipt(RollCallMessage? message, string status, string code) => new()
    {
        MessageId = Guid.TryParse(message?.MessageId, out var id) ? id.ToString("D") : null,
        Status = status,
        Code = code,
        ReceiverInstanceId = InstanceId
    };
}
