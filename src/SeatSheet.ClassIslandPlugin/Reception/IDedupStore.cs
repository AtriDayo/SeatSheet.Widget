namespace SeatSheet.ClassIslandPlugin.Reception;

public sealed record DedupEntry(Guid MessageId, string Fingerprint, DateTimeOffset RetainUntilUtc);

public interface IDedupStore
{
    Task<IReadOnlyList<DedupEntry>> LoadAsync();
    Task SaveAsync(IReadOnlyCollection<DedupEntry> entries);
}

public interface IRollCallNotificationSink
{
    Task SubmitAsync(SeatSheet.RollCall.Protocol.RollCallMessage message);
}
