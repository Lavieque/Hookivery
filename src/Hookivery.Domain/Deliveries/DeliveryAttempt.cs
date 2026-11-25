namespace Hookivery.Domain.Deliveries;

public sealed class DeliveryAttempt
{
    public const int MaxErrorLength = 2000;
    public const int MaxResponseBodyLength = 4096;

    private DeliveryAttempt()
    {
    }

    public Guid Id { get; private init; }

    public Guid DeliveryId { get; private init; }

    /// <summary>1-based. Unique per delivery.</summary>
    public int AttemptNumber { get; private init; }

    public DateTimeOffset StartedAt { get; private init; }

    public TimeSpan Duration { get; private init; }

    public AttemptOutcome Outcome { get; private init; }

    /// <summary>Null when no HTTP response arrived (timeout, network error).</summary>
    public int? StatusCode { get; private init; }

    public string? Error { get; private init; }

    /// <summary>Start of the response body, for diagnosis. May hold destination data: never log it.</summary>
    public string? ResponseBody { get; private init; }

    public DateTimeOffset FinishedAt => StartedAt + Duration;

    public static DeliveryAttempt Record(
        Delivery delivery,
        DateTimeOffset startedAt,
        TimeSpan duration,
        AttemptOutcome outcome,
        int? statusCode,
        string? error,
        string? responseBody)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ValidateOutcome(outcome, statusCode, error, responseBody);

        return new DeliveryAttempt
        {
            Id = Guid.CreateVersion7(startedAt),
            DeliveryId = delivery.Id,
            AttemptNumber = delivery.AttemptCount + 1,
            StartedAt = startedAt,
            Duration = duration,
            Outcome = outcome,
            StatusCode = statusCode,
            Error = Truncate(error, MaxErrorLength),
            ResponseBody = Truncate(responseBody, MaxResponseBodyLength),
        };
    }

    private static void ValidateOutcome(AttemptOutcome outcome, int? statusCode, string? error, string? responseBody)
    {
        var hasSuccessCode = statusCode is >= 200 and <= 299;

        var isConsistent = outcome switch
        {
            AttemptOutcome.Succeeded => hasSuccessCode,
            AttemptOutcome.HttpError => statusCode is >= 100 and <= 599 && !hasSuccessCode,
            AttemptOutcome.Timeout or AttemptOutcome.NetworkError =>
                statusCode is null && responseBody is null && !string.IsNullOrWhiteSpace(error),
            _ => false,
        };

        if (!isConsistent)
        {
            throw new ArgumentException(
                $"Outcome {outcome} is inconsistent with status code '{statusCode}', the error and the response body given.",
                nameof(outcome));
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;

}
