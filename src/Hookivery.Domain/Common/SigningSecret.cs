namespace Hookivery.Domain.Common;

public readonly struct SigningSecret : IEquatable<SigningSecret>
{
    public const int MinLength = 32;

    private readonly string? _value;

    private SigningSecret(string value) => _value = value;

    public bool IsEmpty => _value is null;

    public static SigningSecret FromRawValue(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length < MinLength)
        {
            throw new ArgumentException($"A signing secret must be at least {MinLength} characters.", nameof(value));
        }

        return new SigningSecret(value);
    }

    /// <summary>Returns the raw secret. Only signature code calls this; never log the result.</summary>
    public string Reveal() =>
        _value ?? throw new InvalidOperationException("This secret is empty.");

    public override string ToString() => "***";

    public bool Equals(SigningSecret other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SigningSecret other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_value ?? string.Empty);

    public static bool operator ==(SigningSecret left, SigningSecret right) => left.Equals(right);

    public static bool operator !=(SigningSecret left, SigningSecret right) => !left.Equals(right);
}
