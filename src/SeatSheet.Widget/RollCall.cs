using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using SeatSheet.RollCall.Protocol;

namespace SeatSheet.Widget;

public sealed class ParticipantRule
{
    public int Weight { get; set; } = 1;
    public bool Absent { get; set; }
}
public sealed class RollCallOptions
{
    public string Output { get; set; } = "widget";
    public bool NoRepeat { get; set; }
    public Dictionary<string, Dictionary<string, ParticipantRule>> Classes { get; set; } = new();
    public RollCallOptions Clone() => JsonSerializer.Deserialize<RollCallOptions>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if (Output is not ("widget" or "classIsland" or "both") || Classes == null ||
            Classes.Any(c => c.Value == null || c.Value.Any(r => r.Value == null || r.Value.Weight is < 0 or > 100)))
            throw new ArgumentException("权重需为 0–100 的整数，0 表示不参与。");
    }
}
public sealed class RollCallParticipant
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string StudentNo { get; init; } = "";
    public int Row { get; init; }
    public int Column { get; init; }
    public string SeatLabel => $"第{Row + 1}排第{Column + 1}列";
    public int Weight { get; set; } = 1;
    public bool Absent { get; set; }
}
public static class RollCallRoster
{
    public static string Scope(string source, SeatPlan plan) => JsonSerializer.Serialize(new[] { source, plan.Name });
    public static List<RollCallParticipant> Create(string source, SeatPlan plan, RollCallOptions options)
    {
        plan.Validate();
        var occupied = plan.Seats.Where(s => !string.IsNullOrWhiteSpace(s.Name) || !string.IsNullOrWhiteSpace(s.StudentNo)).ToList();
        string Identity(Seat s) => JsonSerializer.Serialize(new[] { s.StudentNo?.Trim() ?? "", s.Name?.Trim() ?? "" });
        var counts = occupied.GroupBy(Identity).ToDictionary(g => g.Key, g => g.Count());
        options.Classes.TryGetValue(Scope(source, plan), out var rules);
        return occupied.OrderBy(s => s.Row).ThenBy(s => s.Column).Select(s =>
        {
            var id = Identity(s);
            if (counts[id] > 1) id = JsonSerializer.Serialize(new[] { id, $"{s.Row}:{s.Column}" });
            var rule = rules?.GetValueOrDefault(id);
            return new RollCallParticipant { Id = id, Name = string.IsNullOrWhiteSpace(s.Name) ? $"学号 {s.StudentNo!.Trim()}" : s.Name.Trim(),
                StudentNo = s.StudentNo?.Trim() ?? "", Row = s.Row, Column = s.Column,
                Weight = rule?.Weight ?? 1, Absent = rule?.Absent ?? false };
        }).ToList();
    }
    public static void SaveRules(string source, SeatPlan plan, IEnumerable<RollCallParticipant> roster, RollCallOptions options)
    {
        var rules = roster.ToDictionary(p => p.Id, p => new ParticipantRule { Weight = p.Weight, Absent = p.Absent });
        if (rules.Values.Any(r => r.Weight is < 0 or > 100)) throw new ArgumentException("权重需为 0–100 的整数。");
        options.Classes[Scope(source, plan)] = rules;
    }
}
public sealed class RollCallEngine
{
    private readonly Dictionary<string, HashSet<string>> _drawn = new();
    public void ResetRound() => _drawn.Clear();
    public RollCallMessage Draw(string source, SeatPlan plan, RollCallOptions options, Func<long, long>? next = null)
    {
        options.Validate();
        var scope = RollCallRoster.Scope(source, plan);
        if (!_drawn.TryGetValue(scope, out var drawn)) _drawn[scope] = drawn = new();
        var candidates = RollCallRoster.Create(source, plan, options).Where(p => !p.Absent && p.Weight > 0 &&
            (!options.NoRepeat || !drawn.Contains(p.Id))).ToList();
        var total = candidates.Sum(p => (long)p.Weight);
        if (total == 0) throw new InvalidOperationException(options.NoRepeat && drawn.Count > 0
            ? "本轮可参与学生已抽完，或其余学生已排除。请在点名设置中重置本轮。" : "没有可参与的学生。请检查名单、缺席状态和权重。");
        var ticket = next == null ? RandomNumberGenerator.GetInt32((int)total) : next(total);
        if (ticket < 0 || ticket >= total) throw new ArgumentOutOfRangeException(nameof(next));
        RollCallParticipant selected = candidates[0];
        foreach (var p in candidates) { if (ticket < p.Weight) { selected = p; break; } ticket -= p.Weight; }
        var now = DateTimeOffset.UtcNow;
        var message = new RollCallMessage { MessageId = Guid.NewGuid().ToString("D"), CreatedAtUtc = now, ExpiresAtUtc = now.AddSeconds(60),
            Student = new() { Name = selected.Name, ClassName = plan.Name, Seat = new() { Row = selected.Row + 1, Column = selected.Column + 1 } } };
        if (options.NoRepeat) drawn.Add(selected.Id);
        return message;
    }
}
