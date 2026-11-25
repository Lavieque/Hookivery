using Hookivery.Domain.Common;

namespace Hookivery.Domain.Sources;

public sealed class Source
{
    public const int MaxNameLength = 200;

    private Source(Guid id, string name, SigningSecret secret, bool isActive, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Secret = secret;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public SigningSecret Secret { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Source Register(string name, SigningSecret secret, DateTimeOffset now)
    {
        var trimmed = ValidateName(name);

        if (secret.IsEmpty)
        {
            throw new ArgumentException("A source requires a secret.", nameof(secret));
        }

        return new Source(Guid.CreateVersion7(now), trimmed, secret, isActive: true, now);
    }

    public void Rename(string name) => Name = ValidateName(name);

    public void RotateSecret(SigningSecret secret)
    {
        if (secret.IsEmpty)
        {
            throw new ArgumentException("A source requires a secret.", nameof(secret));
        }

        Secret = secret;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException($"A source name must not exceed {MaxNameLength} characters.", nameof(name));
        }

        return trimmed;
    }
}
