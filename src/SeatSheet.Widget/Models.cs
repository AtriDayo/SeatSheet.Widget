using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SeatSheet.Widget;

public sealed class Seat
{
    public int Row { get; set; }
    public int Column { get; set; }
    public string? Name { get; set; }
    public string? StudentNo { get; set; }
}
public sealed class SeatPlan
{
    public string Name { get; set; } = "座位表";
    public int Rows { get; set; }
    public int Columns { get; set; }
    public string DoorSide { get; set; } = "right";
    public List<int> AisleAfterColumns { get; set; } = new();
    public bool ShowStudentNo { get; set; }
    public List<Seat> Seats { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; }
    public void Validate()
    {
        if (Rows is < 1 or > 30 || Columns is < 1 or > 30 || Seats == null || AisleAfterColumns == null)
            throw new InvalidDataException("座位表格式不正确，请检查服务地址。");
        if (Seats.Any(s => s == null || s.Row < 0 || s.Row >= Rows || s.Column < 0 || s.Column >= Columns) ||
            Seats.Select(s => (s.Row, s.Column)).Distinct().Count() != Seats.Count)
            throw new InvalidDataException("座位表包含无效或重复的位置。");
    }
}
public sealed class WidgetSettings
{
    public string ServerUrl { get; set; } = "http://localhost:5173";
    public double ButtonTopRatio { get; set; } = 0.42;
    public double PanelWidth { get; set; } = 920;
    public string LauncherStyle { get; set; } = LauncherStyles.Label;
    public RollCallOptions RollCall { get; set; } = new();
}
public static class LauncherStyles
{
    public const string Label = "label", Slim = "slim", Arrow = "arrow";
    public static string Normalize(string? value) => value is Slim or Arrow ? value : Label;
}
public sealed class CachedPlan
{
    public string Source { get; set; } = "";
    public DateTimeOffset FetchedAt { get; set; }
    public SeatPlan Plan { get; set; } = new();
}
internal static class LocalStore
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    internal static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeatSheet.Widget");
    public static WidgetSettings LoadSettings()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(Path.Combine(Folder, "settings.json")), Json) ?? new();
            settings.ServerUrl = SeatApi.Normalize(settings.ServerUrl);
            settings.ButtonTopRatio = double.IsFinite(settings.ButtonTopRatio) ? Math.Clamp(settings.ButtonTopRatio, 0, 1) : 0.42;
            settings.PanelWidth = double.IsFinite(settings.PanelWidth) ? Math.Clamp(settings.PanelWidth, 480, 1800) : 920;
            settings.LauncherStyle = LauncherStyles.Normalize(settings.LauncherStyle);
            settings.RollCall ??= new();
            try { settings.RollCall.Validate(); } catch { settings.RollCall = new(); }
            return settings;
        }
        catch { return new(); }
    }
    public static void SaveSettings() => Write("settings.json", App.Settings);
    public static CachedPlan? ReadCache(string source)
    {
        try
        {
            var cached = JsonSerializer.Deserialize<CachedPlan>(File.ReadAllText(Path.Combine(Folder, "cache.json")), Json);
            if (cached?.Source != source) return null;
            cached.Plan.Validate();
            return cached;
        }
        catch { return null; }
    }
    public static void SaveCache(CachedPlan cached) => Write("cache.json", cached);
    private static void Write<T>(string name, T value)
    {
        if (App.IsSmoke) return;
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, name);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value, Json));
        File.Move(path + ".tmp", path, true);
    }
}
internal static class SeatApi
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    public static string Normalize(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("请输入完整的 http:// 或 https:// 网站地址，不含查询参数。");
        var result = uri.AbsoluteUri.TrimEnd('/');
        if (result.EndsWith("/api/seat-plan", StringComparison.OrdinalIgnoreCase)) result = result[..^14];
        else if (result.EndsWith("/api", StringComparison.OrdinalIgnoreCase)) result = result[..^4];
        else if (result.EndsWith("/config", StringComparison.OrdinalIgnoreCase)) result = result[..^7];
        return result;
    }
    public static async Task<CachedPlan> Fetch(string source)
    {
        using var response = await Http.GetAsync(source + "/api/seat-plan");
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"服务返回 {(int)response.StatusCode}，请检查地址或稍后重试。");
        var plan = JsonSerializer.Deserialize<SeatPlan>(await response.Content.ReadAsStringAsync(), LocalStore.Json)
            ?? throw new InvalidDataException("服务器没有返回座位表。");
        plan.Validate();
        return new CachedPlan { Source = source, Plan = plan, FetchedAt = DateTimeOffset.Now };
    }
}
