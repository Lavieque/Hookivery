using Hookivery.Domain.Common;
using Hookivery.Domain.Events;

namespace Hookivery.Domain.Destinations;

public sealed class Destination
{
    public const int MaxNameLength = 200;
    public const int MaxUrlLength = 2048;
    public const int MaxEventTypes = 50;

    private List<string> _eventTypes = [];

    private Destination(Guid id, Guid sourceId, string name, Uri url, DateTimeOffset createdAt)
    {
        Id = id;
        SourceId = sourceId;
        Name = name;
        Url = url;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public Guid SourceId { get; private set; }

    public string Name { get; private set; }

    public Uri Url { get; private set; }

    /// <summary>Signs outgoing requests so the destination can verify them. Never returned by the API (TASK-010).</summary>
    public SigningSecret Secret { get; private set; }

    public TimeSpan Timeout { get; private set; }

    public int MaxAttempts { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<string> EventTypes => _eventTypes.AsReadOnly();

    public static Destination Register(
        Guid sourceId,
        string name,
        Uri url,
        SigningSecret secret,
        DeliverySettings settings,
        IEnumerable<string> eventTypes,
        DateTimeOffset now)
    {
        if (sourceId == Guid.Empty)
        {
            throw new ArgumentException("A destination requires a source.", nameof(sourceId));
        }

        ArgumentNullException.ThrowIfNull(settings);

        return new Destination(Guid.CreateVersion7(now), sourceId, ValidateName(name), ValidateUrl(url), now)
        {
            Secret = ValidateSecret(secret),
            Timeout = settings.Timeout,
            MaxAttempts = settings.MaxAttempts,
            _eventTypes = NormalizeEventTypes(eventTypes),
        };
    }

    public void Rename(string name) => Name = ValidateName(name);

    public void ChangeUrl(Uri url) => Url = ValidateUrl(url);

    public void ChangeDeliverySettings(DeliverySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Timeout = settings.Timeout;
        MaxAttempts = settings.MaxAttempts;
    }

    public void RotateSecret(SigningSecret secret) => Secret = ValidateSecret(secret);

    public void ReplaceSubscriptions(IEnumerable<string> eventTypes) =>
        _eventTypes = NormalizeEventTypes(eventTypes);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public bool SubscribesTo(string eventType) =>
        IsActive
        && !string.IsNullOrWhiteSpace(eventType)
        && _eventTypes.Contains(EventTypeName.Normalize(eventType), StringComparer.Ordinal);

    private static SigningSecret ValidateSecret(SigningSecret secret) =>
        secret.IsEmpty
            ? throw new ArgumentException("A destination requires a secret.", nameof(secret))
            : secret;

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"A destination name must not exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return trimmed;
    }

    private static Uri ValidateUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("A destination URL must be absolute.", nameof(url));
        }

        var isHttp = string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            || string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

        if (!isHttp)
        {
            throw new ArgumentException("A destination URL must use http or https.", nameof(url));
        }

        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            throw new ArgumentException("A destination URL must not contain credentials.", nameof(url));
        }

        if (url.AbsoluteUri.Length > MaxUrlLength)
        {
            throw new ArgumentException(
                $"A destination URL must not exceed {MaxUrlLength} characters.",
                nameof(url));
        }

        return url;
    }

    private static List<string> NormalizeEventTypes(IEnumerable<string> eventTypes)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);

        var normalized = eventTypes
            .Select(EventTypeName.Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count > MaxEventTypes)
        {
            throw new ArgumentException(
                $"A destination must not subscribe to more than {MaxEventTypes} event types.",
                nameof(eventTypes));
        }

        return normalized;
    }
}
