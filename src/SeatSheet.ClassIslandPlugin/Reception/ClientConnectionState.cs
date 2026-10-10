namespace SeatSheet.ClassIslandPlugin.Reception;

// A live client sends hello every 10 seconds; idle pipe handles alone do not imply liveness.
public sealed class ClientConnectionState(TimeProvider? timeProvider = null)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private long _lastContact;
    private int _seen;
    public bool Connected => Volatile.Read(ref _seen) != 0 &&
        _time.GetElapsedTime(Interlocked.Read(ref _lastContact)) < Timeout;
    public void RecordContact()
    {
        Interlocked.Exchange(ref _lastContact, _time.GetTimestamp());
        Volatile.Write(ref _seen, 1);
    }
}
