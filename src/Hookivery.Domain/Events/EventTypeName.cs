using System.Diagnostics.CodeAnalysis;

namespace Hookivery.Domain.Events;

public static class EventTypeName
{
    public const int MaxLength = 200;

    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Event types are stored lowercase so the domain rule and the SQL fan-out filter match.")]
    public static string Normalize(string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        var normalized = eventType.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new ArgumentException($"An event type must not exceed {MaxLength} characters.", nameof(eventType));
        }

        return normalized;
    }
}
