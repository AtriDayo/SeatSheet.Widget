using System.Text.Json;

namespace SeatSheet.ClassIslandPlugin.Reception;

public sealed class FileDedupStore(string path) : IDedupStore
{
    public async Task<IReadOnlyList<DedupEntry>> LoadAsync()
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 256 * 1024) throw new InvalidDataException("Dedup journal too large.");
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<DedupEntry>>(stream)
            ?? throw new InvalidDataException("Invalid dedup journal.");
    }

    public async Task SaveAsync(IReadOnlyCollection<DedupEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporaryPath = path + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                         4096, FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, entries);
            await stream.FlushAsync();
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporaryPath, path, overwrite: true);
    }
}
