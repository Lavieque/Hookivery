using Hookivery.Domain.Common;

namespace Hookivery.UnitTests;

public sealed class SigningSecretTests
{
    [Fact]
    public void ToStringNeverRevealsValue()
    {
        var secret = TestData.NewSecret();

        Assert.Equal("***", secret.ToString());
        Assert.Equal("***", $"{secret}");
    }

    [Fact]
    public void DefaultIsEmptyAndCannotBeRevealed()
    {
        var secret = default(SigningSecret);

        Assert.True(secret.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => secret.Reveal());
    }

    [Fact]
    public void FromRawValueRejectsShortValue()
    {
        var value = new string('s', SigningSecret.MinLength - 1);

        Assert.Throws<ArgumentException>(() => SigningSecret.FromRawValue(value));
    }
}
