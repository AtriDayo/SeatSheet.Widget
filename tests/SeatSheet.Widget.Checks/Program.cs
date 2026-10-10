using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SeatSheet.Widget;
// The core model/service code has no UI dependency; use isolated test storage.
internal static class App { public static bool IsSmoke => true; public static WidgetSettings Settings { get; } = new(); }
internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    private static void Reject(Action action, string name)
    {
        try { action(); } catch (ArgumentException) { Check(true, name); return; } catch (InvalidDataException) { Check(true, name); return; }
        throw new Exception("Expected rejection: " + name);
    }
    public static async Task Main()
    {
        Check(System.Text.Json.JsonSerializer.Deserialize<WidgetSettings>("{}")!.LauncherStyle == LauncherStyles.Label, "Legacy settings retain the labeled handle");
        Check(LauncherStyles.Normalize("unknown") == LauncherStyles.Label, "Unknown handle style falls back without discarding settings");
        foreach (var style in new[] { LauncherStyles.Label, LauncherStyles.Slim, LauncherStyles.Arrow })
            Check(System.Text.Json.JsonSerializer.Deserialize<WidgetSettings>(System.Text.Json.JsonSerializer.Serialize(new WidgetSettings { LauncherStyle = style }))!.LauncherStyle == style, "Persist handle style " + style);
        foreach (var suffix in new[] { "", "/", "/api", "/api/seat-plan", "/config" })
            Check(SeatApi.Normalize("https://example.com" + suffix) == "https://example.com", "Normalize " + suffix);
        Check(SeatApi.Normalize(" https://example.com/school/api ") == "https://example.com/school", "Keep deployment prefix");
        foreach (var address in new[] { "example.com", "file:///C:/seats.json", "https://user:secret@example.com", "https://example.com?token=x", "https://example.com#x" })
            Reject(() => SeatApi.Normalize(address), "Reject unsafe/ambiguous address");
        Reject(() => new SeatPlan { Rows = 0, Columns = 8 }.Validate(), "Reject invalid dimensions");
        Reject(() => new SeatPlan { Rows = 2, Columns = 2, Seats = new List<Seat> { new() { Row = 2 } } }.Validate(), "Reject out-of-range cell");
        Reject(() => new SeatPlan { Rows = 2, Columns = 2, Seats = new List<Seat> { new(), new() } }.Validate(), "Reject duplicate cell");
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:18763/"); listener.Start();
        var server = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            Check(context.Request.HttpMethod == "GET" && context.Request.RawUrl == "/api/seat-plan", "Read-only API contract");
            var bytes = Encoding.UTF8.GetBytes("{\"name\":\"测试班级\",\"rows\":1,\"columns\":2,\"doorSide\":\"left\",\"aisleAfterColumns\":[0],\"showStudentNo\":true,\"updatedAt\":\"2026-10-09T00:00:00Z\",\"seats\":[{\"row\":0,\"column\":0,\"name\":\"测试姓名\",\"studentNo\":\"01\"}]}");
            context.Response.ContentType = "application/json"; context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
        });
        var result = await SeatApi.Fetch("http://localhost:18763"); await server;
        Check(result.Plan.Name == "测试班级" && result.Plan.Seats[0].StudentNo == "01" && result.Plan.ShowStudentNo, "Deserialize Chinese names and display settings");
        listener.Stop();
        try { await SeatApi.Fetch("http://localhost:18763"); throw new Exception("Expected offline failure"); }
        catch (System.Net.Http.HttpRequestException) { Check(result.Plan.Seats[0].Name == "测试姓名", "Connection failure leaves prior result intact"); }
        Console.WriteLine($"{checks} checks passed.");
    }
}
