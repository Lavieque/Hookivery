using Hookivery.Domain.Sources;

namespace Hookivery.UnitTests;

public sealed class SourceTests
{
    [Fact]
    public void RegisterTrimsNameAndStartsActive()
    {
        var source = Source.Register("  Billing  ", TestData.NewSecret(), TestData.Now);

        Assert.Equal("Billing", source.Name);
        Assert.True(source.IsActive);
        Assert.Equal(TestData.Now, source.CreatedAt);
    }

    [Fact]
    public void RegisterRejectsMissingSecret()
    {
        Assert.Throws<ArgumentException>(() => Source.Register("Billing", default, TestData.Now));
    }

    [Fact]
    public void RotateSecretRejectsMissingSecret()
    {
        var source = TestData.NewSource();

        Assert.Throws<ArgumentException>(() => source.RotateSecret(default));
    }

    [Fact]
    public void RegisterRejectsTooLongName()
    {
        var name = new string('n', Source.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => Source.Register(name, TestData.NewSecret(), TestData.Now));
    }
}
