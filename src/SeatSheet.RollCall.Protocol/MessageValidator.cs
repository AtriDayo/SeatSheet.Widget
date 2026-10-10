using System.Security.Cryptography;
using System.Text.Json;

namespace SeatSheet.RollCall.Protocol;

public static class MessageValidator
{
    public static string? Validate(RollCallMessage? message, DateTimeOffset now)
    {
        if (message is null) return "missingMessage";
        if (message.ProtocolVersion != RollCallProtocol.Version) return "unsupportedVersion";
        if (!Guid.TryParseExact(message.MessageId, "D", out var id) || id == Guid.Empty) return "invalidMessageId";
        if (message.Type != "rollCallResult") return "invalidType";
        if (message.CreatedAtUtc == default || message.ExpiresAtUtc == default ||
            message.CreatedAtUtc.Offset != TimeSpan.Zero || message.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            message.ExpiresAtUtc <= message.CreatedAtUtc ||
            message.ExpiresAtUtc - message.CreatedAtUtc > RollCallProtocol.MaxLifetime ||
            message.CreatedAtUtc > now + RollCallProtocol.ClockTolerance) return "invalidTime";
        if (message.Student is null || !TextValid(message.Student.Name, 64, required: true)) return "invalidName";
        if (!TextValid(message.Student.ClassName, 128)) return "invalidClassName";
        if (message.Student.Seat is { } seat &&
            (!TextValid(seat.Label, 128) || seat.Row is < 1 or > 30 || seat.Column is < 1 or > 30 ||
             seat.Row.HasValue != seat.Column.HasValue ||
             (seat.Row is null && string.IsNullOrWhiteSpace(seat.Label)))) return "invalidSeat";
        if (JsonSerializer.SerializeToUtf8Bytes(message).Length > RollCallProtocol.MaxMessageBytes) return "messageTooLarge";
        if (message.ExpiresAtUtc <= now) return "expired";
        return null;
    }

    public static string Fingerprint(RollCallMessage message) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(message with
        {
            MessageId = Guid.Parse(message.MessageId!).ToString("D")
        })));

    private static bool TextValid(string? text, int limit, bool required = false) =>
        (text is null ? !required : text.Length <= limit && !text.Any(char.IsControl) &&
            (!required || !string.IsNullOrWhiteSpace(text)));
}
