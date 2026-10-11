using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

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
        Check(!System.Text.Json.JsonSerializer.Deserialize<WidgetSettings>("{}")!.DynamicResizeAnimation, "Legacy settings disable live resize animation by default");
        Check(!System.Text.Json.JsonSerializer.Deserialize<WidgetSettings>(System.Text.Json.JsonSerializer.Serialize(new WidgetSettings { DynamicResizeAnimation=false }))!.DynamicResizeAnimation, "Disabled resize animation survives settings serialization");
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
        var plan = new SeatPlan { Name = "示例班级", Rows = 2, Columns = 3, Seats = new()
        {
            new() { Row=0, Column=0, Name="示例甲", StudentNo="01" },
            new() { Row=0, Column=1, Name="示例乙", StudentNo="02" },
            new() { Row=0, Column=2, Name=" " },
            new() { Row=1, Column=0, Name="示例丙", StudentNo="03" }
        } };
        var options = new RollCallOptions();
        var roster = RollCallRoster.Create("https://example.com", plan, options);
        Check(roster.Count == 3 && roster.All(p => p.Weight == 1), "Empty seats excluded; all occupants default to weight one");
        roster[1].Weight = 3; roster[2].Absent = true;
        RollCallRoster.SaveRules("https://example.com", plan, roster, options);
        var engine = new RollCallEngine();
        var weighted = Enumerable.Range(0,4).Select(i => engine.Draw("https://example.com",plan,options,_=>i)).ToList();
        Check(weighted[0].Student!.Name == "示例甲" && weighted.Skip(1).All(m=>m.Student!.Name=="示例乙"), "Every weighted ticket maps to the expected student; absent students excluded");
        Check(weighted.All(m=>m.Student!.Seat!.Row==1) && weighted.Select(m=>m.MessageId).Distinct().Count()==4, "Draws have unique IDs and convert zero-based seats to one-based protocol");
        roster[0].Weight=0; RollCallRoster.SaveRules("https://example.com",plan,roster,options);
        Check(engine.Draw("https://example.com",plan,options,_=>0).Student!.Name=="示例乙", "Weight zero excludes an occupied seat");
        options.NoRepeat=true;
        engine.Draw("https://example.com",plan,options,_=>0);
        var exhausted=false; try { engine.Draw("https://example.com",plan,options,_=>0); } catch(InvalidOperationException) { exhausted=true; }
        Check(exhausted,"Exhausted round requires explicit reset rather than silently repeating");
        engine.ResetRound();
        Check(engine.Draw("https://example.com",plan,options,_=>0).Student!.Name=="示例乙","Reset permits students in a new round");
        var clone=options.Clone(); clone.Classes[RollCallRoster.Scope("https://example.com",plan)][roster[1].Id].Weight=7;
        Check(options.Classes[RollCallRoster.Scope("https://example.com",plan)][roster[1].Id].Weight==3,"Editing draft settings cannot change active weights");
        var restored=System.Text.Json.JsonSerializer.Deserialize<RollCallOptions>(System.Text.Json.JsonSerializer.Serialize(options))!;
        Check(RollCallRoster.Create("https://example.com",plan,restored)[2].Absent,"Absence and weights survive settings serialization");
        Check(RollCallRoster.Create("https://other.example.com",plan,restored).All(p=>p.Weight==1&&!p.Absent),"Different website does not inherit another class's rules");
        plan.Seats[1].Row=1; plan.Seats[1].Column=1;
        Check(RollCallRoster.Create("https://example.com",plan,restored).Single(p=>p.StudentNo=="02").Weight==3,"Moving an identified student retains their weight");
        var duplicateNames = new SeatPlan { Rows=1,Columns=2,Seats=new() {new() {Name="示例同名"},new() {Name="示例同名",Column=1}} };
        Check(RollCallRoster.Create("https://example.com",duplicateNames,new()).Select(p=>p.Id).Distinct().Count()==2,"Ambiguous duplicate names retain distinct seat identities");
        var localOnlyPlan = new SeatPlan { Rows=1,Columns=1,Name=new string('示',129),Seats=new() {new() {Name="示例同学"}} };
        Check(new RollCallEngine().Draw("https://example.com",localOnlyPlan,new()).Student!.Name=="示例同学","Plugin format limits cannot disable local drawing");
        Reject(()=>{ clone.Classes[RollCallRoster.Scope("https://example.com",plan)][roster[1].Id].Weight=101;clone.Validate(); },"Reject invalid persisted weights");
        Console.WriteLine($"{checks} checks passed.");
    }
}
