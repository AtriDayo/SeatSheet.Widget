using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace SeatSheet.RollCall.Protocol;

public static class RollCallProtocol
{
    public const int Version = 1;
    public const string HelloRoute = "seatsheet.widget/rollcall/v1/hello";
    public const string NotifyRoute = "seatsheet.widget/rollcall/v1/notify";
    public const int MaxMessageBytes = 8192;
    public const int MaxPendingRequests = 8;
    public const int MaxDedupEntries = 1024;
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DedupGracePeriod = TimeSpan.FromMinutes(5);
}

public sealed record RollCallMessage
{
    [JsonProperty("protocolVersion"), JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; } = RollCallProtocol.Version;
    [JsonProperty("messageId"), JsonPropertyName("messageId")]
    public string? MessageId { get; init; }
    [JsonProperty("type"), JsonPropertyName("type")]
    public string? Type { get; init; } = "rollCallResult";
    [JsonProperty("createdAtUtc"), JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }
    [JsonProperty("expiresAtUtc"), JsonPropertyName("expiresAtUtc")]
    public DateTimeOffset ExpiresAtUtc { get; init; }
    [JsonProperty("student"), JsonPropertyName("student")]
    public RollCallStudent? Student { get; init; }
}

public sealed record RollCallStudent
{
    [JsonProperty("name"), JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonProperty("className"), JsonPropertyName("className")]
    public string? ClassName { get; init; }
    [JsonProperty("seat"), JsonPropertyName("seat")]
    public RollCallSeat? Seat { get; init; }
}

public sealed record RollCallSeat
{
    [JsonProperty("row"), JsonPropertyName("row")]
    public int? Row { get; init; }
    [JsonProperty("column"), JsonPropertyName("column")]
    public int? Column { get; init; }
    [JsonProperty("label"), JsonPropertyName("label")]
    public string? Label { get; init; }
}

public sealed record RollCallReceipt
{
    [JsonProperty("protocolVersion"), JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; } = RollCallProtocol.Version;
    [JsonProperty("messageId"), JsonPropertyName("messageId")]
    public string? MessageId { get; init; }
    [JsonProperty("status"), JsonPropertyName("status")]
    public string Status { get; init; } = "internalError";
    [JsonProperty("code"), JsonPropertyName("code")]
    public string? Code { get; init; }
    [JsonProperty("receiverInstanceId"), JsonPropertyName("receiverInstanceId")]
    public string ReceiverInstanceId { get; init; } = "";
}

public sealed record RollCallHello
{
    [JsonProperty("protocolVersion"), JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; } = RollCallProtocol.Version;
    [JsonProperty("pluginVersion"), JsonPropertyName("pluginVersion")]
    public string PluginVersion { get; init; } = "0.1.5";
    [JsonProperty("receiverInstanceId"), JsonPropertyName("receiverInstanceId")]
    public string ReceiverInstanceId { get; init; } = "";
    [JsonProperty("ready"), JsonPropertyName("ready")]
    public bool Ready { get; init; }
}
