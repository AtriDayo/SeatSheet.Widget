using System.Text.Json;

namespace SeatSheet.ClassIslandPlugin.Presentation;

public sealed record NotificationDisplaySettings
{
    public const int DefaultSeconds = 5;
    public const int MinimumSeconds = 2;
    public const int MaximumSeconds = 30;
    public int DurationSeconds { get; init; } = DefaultSeconds;
    public static bool IsValid(int seconds) => seconds is >= MinimumSeconds and <= MaximumSeconds;
}

public sealed class NotificationDisplaySettingsStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _seconds = NotificationDisplaySettings.DefaultSeconds;
    public int DurationSeconds => Volatile.Read(ref _seconds);
    public bool LoadFailed { get; private set; }

    public NotificationDisplaySettingsStore(string path)
    {
        _path = path;
        try
        {
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length > 16 * 1024) throw new InvalidDataException("Display settings too large.");
            var settings = JsonSerializer.Deserialize<NotificationDisplaySettings>(File.ReadAllText(path));
            if (settings is null || !NotificationDisplaySettings.IsValid(settings.DurationSeconds))
                throw new InvalidDataException("Invalid display duration.");
            _seconds = settings.DurationSeconds;
        }
        catch { LoadFailed = true; }
    }

    public async Task SaveDurationAsync(int seconds)
    {
        if (!NotificationDisplaySettings.IsValid(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        await _gate.WaitAsync();
        try
        {
            // Compare under the gate because an earlier save may still be publishing a value.
            if (seconds == DurationSeconds && !LoadFailed && File.Exists(_path)) return;
            var fullPath = Path.GetFullPath(_path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using (var stream = new FileStream(fullPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None,
                             4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, new NotificationDisplaySettings { DurationSeconds = seconds });
                await stream.FlushAsync();
                stream.Flush(flushToDisk: true);
            }
            File.Move(fullPath + ".tmp", fullPath, overwrite: true);
            Volatile.Write(ref _seconds, seconds);
            LoadFailed = false;
        }
        finally { _gate.Release(); }
    }
}

public readonly record struct NotificationTiming(int MaskSeconds, int OverlaySeconds)
{
    public static NotificationTiming Create(int totalSeconds, bool hasDetails)
    {
        if (!NotificationDisplaySettings.IsValid(totalSeconds)) throw new ArgumentOutOfRangeException(nameof(totalSeconds));
        if (!hasDetails) return new(totalSeconds, 0);
        var maskSeconds = Math.Min(2, totalSeconds / 2);
        return new(maskSeconds, totalSeconds - maskSeconds);
    }
}
