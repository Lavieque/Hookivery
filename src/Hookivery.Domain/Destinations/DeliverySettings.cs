namespace Hookivery.Domain.Destinations;

public sealed record DeliverySettings
{
    public const int MinAttempts = 1;
    public const int MaxAttemptsLimit = 20;

    public static readonly TimeSpan MinTimeout = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(60);

    public DeliverySettings(TimeSpan timeout, int maxAttempts)
    {
        if (timeout < MinTimeout || timeout > MaxTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                $"The timeout must be between {MinTimeout.TotalSeconds}s and {MaxTimeout.TotalSeconds}s.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, MinAttempts);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxAttempts, MaxAttemptsLimit);

        Timeout = timeout;
        MaxAttempts = maxAttempts;
    }

    public TimeSpan Timeout { get; }

    public int MaxAttempts { get; }
}
