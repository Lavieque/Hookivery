using System.Diagnostics.CodeAnalysis;

using Hookivery.Domain.Destinations;
using Hookivery.Domain.Events;

namespace Hookivery.Domain.Deliveries;

public sealed class Delivery
{
    public const int MaxErrorLength = DeliveryAttempt.MaxErrorLength;

    private Delivery(Guid id, Guid eventId, Guid destinationId, DateTimeOffset createdAt)
    {
        Id = id;
        EventId = eventId;
        DestinationId = destinationId;
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
        Status = DeliveryStatus.Pending;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public Guid DestinationId { get; private set; }

    public DeliveryStatus Status { get; private set; }

    /// <summary>Attempts recorded so far. Equals the number of the last attempt.</summary>
    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>Start of the last recorded attempt.</summary>
    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>HTTP status of the last attempt; null when no response arrived.</summary>
    public int? LastStatusCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Reason of the last failure; for a dead letter, why it ended (TASK-023).</summary>
    public string? LastError { get; private set; }

    /// <summary>Set by the claim SQL in Infrastructure (ADR-0004); the domain only clears it.</summary>
    public string? ClaimedBy { get; private set; }

    /// <summary>Set by the claim SQL in Infrastructure (ADR-0004); the domain only clears it.</summary>
    public DateTimeOffset? ClaimedUntil { get; private set; }

    /// <summary>Times the delivery was claimed. Higher than AttemptCount means claim churn.</summary>
    [SuppressMessage(
        "Major Code Smell",
        "S1144:Unused private types or members should be removed",
        Justification = "Written by EF Core on materialization; only the claim SQL changes it (ADR-0004).")]
    public int ClaimCount { get; private set; }

    public bool IsTerminal => Status is DeliveryStatus.Succeeded or DeliveryStatus.Failed;

    /// <summary>
    /// Creates the delivery of an event to one destination during fan-out.
    /// The SQL fan-out of TASK-018 must enforce the same three rules (ADR-0005).
    /// </summary>
    public static Delivery Create(Event webhookEvent, Destination destination, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);
        ArgumentNullException.ThrowIfNull(destination);

        if (webhookEvent.Status != EventStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Event {webhookEvent.Id} is {webhookEvent.Status}; deliveries are created only during fan-out.");
        }

        if (destination.SourceId != webhookEvent.SourceId)
        {
            throw new InvalidOperationException(
                $"Destination {destination.Id} does not belong to the source of event {webhookEvent.Id}.");
        }

        if (!destination.SubscribesTo(webhookEvent.EventType))
        {
            throw new InvalidOperationException(
                $"Destination {destination.Id} does not subscribe to '{webhookEvent.EventType}'.");
        }

        return new Delivery(Guid.CreateVersion7(now), webhookEvent.Id, destination.Id, now);
    }

    public void Succeed(DeliveryAttempt attempt)
    {
        EnsureCanRecord(attempt);

        if (attempt.Outcome != AttemptOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Attempt outcome {attempt.Outcome} is not a success.");
        }

        ApplyAttempt(attempt);
        Status = DeliveryStatus.Succeeded;
        CompletedAt = attempt.FinishedAt;
        LastError = null;
    }

    /// <summary>Records a failed attempt and keeps the delivery pending until <paramref name="nextAttemptAt"/>.</summary>
    /// <param name="attempt">The failed attempt just made.</param>
    /// <param name="nextAttemptAt">Computed by the retry policy (TASK-022), not here.</param>
    public void ScheduleRetry(DeliveryAttempt attempt, DateTimeOffset nextAttemptAt)
    {
        EnsureCanRecord(attempt);
        EnsureFailure(attempt);

        if (nextAttemptAt < attempt.FinishedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextAttemptAt),
                nextAttemptAt,
                "The next attempt cannot be scheduled before the current one finished.");
        }

        ApplyAttempt(attempt);
        NextAttemptAt = nextAttemptAt;
        LastError = Describe(attempt);
    }

    public void Fail(DeliveryAttempt attempt, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureCanRecord(attempt);
        EnsureFailure(attempt);

        ApplyAttempt(attempt);
        Status = DeliveryStatus.Failed;
        CompletedAt = attempt.FinishedAt;
        LastError = Truncate($"{reason}: {Describe(attempt)}");
    }

    /// <summary>Validates only. Every check runs before any state changes.</summary>
    private void EnsureCanRecord(DeliveryAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (IsTerminal)
        {
            throw new InvalidOperationException($"Delivery {Id} is already {Status}.");
        }

        if (attempt.DeliveryId != Id)
        {
            throw new ArgumentException(
                $"Attempt belongs to delivery {attempt.DeliveryId}, not {Id}.",
                nameof(attempt));
        }

        if (attempt.AttemptNumber != AttemptCount + 1)
        {
            throw new ArgumentException(
                $"Expected attempt {AttemptCount + 1}, got {attempt.AttemptNumber}.",
                nameof(attempt));
        }
    }

    /// <summary>Mutates only. Called after every validation passed.</summary>
    private void ApplyAttempt(DeliveryAttempt attempt)
    {
        AttemptCount = attempt.AttemptNumber;
        LastAttemptAt = attempt.StartedAt;
        LastStatusCode = attempt.StatusCode;
        ClaimedBy = null;
        ClaimedUntil = null;
    }

    private static void EnsureFailure(DeliveryAttempt attempt)
    {
        if (attempt.Outcome == AttemptOutcome.Succeeded)
        {
            throw new InvalidOperationException("A successful attempt cannot be recorded as a failure.");
        }
    }

    private static string Describe(DeliveryAttempt attempt)
    {
        var status = attempt.StatusCode is { } code ? $" (HTTP {code})" : string.Empty;
        var error = string.IsNullOrWhiteSpace(attempt.Error) ? string.Empty : $": {attempt.Error}";

        return Truncate($"{attempt.Outcome}{status}{error}");
    }

    private static string Truncate(string value) =>
        value.Length > MaxErrorLength ? value[..MaxErrorLength] : value;
}
