using System.Diagnostics.CodeAnalysis;

using Hookivery.Domain.Sources;

namespace Hookivery.Domain.Events;
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "C#-only domain; 'Event' is the domain term used by the plan, ADR-0005 and the events table.")]
public sealed class Event
{
    public const int MaxExternalIdLength = 200;

    private Event(
        Guid id,
        Guid sourceId,
        string externalId,
        string eventType,
        string payload,
        DateTimeOffset receivedAt)
    {
        Id = id;
        SourceId = sourceId;
        ExternalId = externalId;
        EventType = eventType;
        Payload = payload;
        ReceivedAt = receivedAt;
        Status = EventStatus.Pending;
    }

    public Guid Id { get; private set; }

    public Guid SourceId { get; private set; }
    public string ExternalId { get; private set; }
    public string EventType { get; private set; }
    public string Payload { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public EventStatus Status { get; private set; }

    public static Event Receive(
        Source source,
        string externalId,
        string eventType,
        string payload,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);
        
        if (!source.IsActive)
        {
            throw new InvalidOperationException($"Source {source.Id} is not active.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var trimmedExternalId = externalId.Trim();

        if (trimmedExternalId.Length > MaxExternalIdLength)
        {
            throw new ArgumentException(
                $"An external id must not exceed {MaxExternalIdLength} characters.",
                nameof(externalId));
        }

        return new Event(
            Guid.CreateVersion7(now),
            source.Id,
            trimmedExternalId,
            EventTypeName.Normalize(eventType),
            payload,
            now);
    }

    public void MarkDispatched() => CompleteFanOut(EventStatus.Dispatched);

    public void MarkNoDestinations() => CompleteFanOut(EventStatus.NoDestinations);

    private void CompleteFanOut(EventStatus target)
    {
        if (Status != EventStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Event {Id} cannot move from {Status} to {target}; fan-out already happened.");
        }

        Status = target;
    }
}
