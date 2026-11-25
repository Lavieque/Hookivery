using Hookivery.Domain.Destinations;

namespace Hookivery.UnitTests;

public sealed class DestinationTests
{
    private static readonly string[] ExpectedTypes = ["order.created", "order.paid"];

    [Fact]
    public void EmptySubscriptionMatchesNothing()
    {
        var destination = TestData.NewDestination(TestData.NewSource());

        Assert.False(destination.SubscribesTo(TestData.OrderCreated));
    }

    [Fact]
    public void InactiveDestinationMatchesNothing()
    {
        var destination = TestData.NewDestination(TestData.NewSource(), TestData.OrderCreated);

        destination.Deactivate();

        Assert.False(destination.SubscribesTo(TestData.OrderCreated));
    }

    [Fact]
    public void MatchIgnoresCaseAndSpacesOnBothSides()
    {
        var destination = TestData.NewDestination(TestData.NewSource(), "Order.Created");

        Assert.True(destination.SubscribesTo("  ORDER.created "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankEventTypeMatchesNothing(string eventType)
    {
        var destination = TestData.NewDestination(TestData.NewSource(), TestData.OrderCreated);

        Assert.False(destination.SubscribesTo(eventType));
    }

    [Fact]
    public void RegisterRemovesDuplicateEventTypes()
    {
        var destination = TestData.NewDestination(
            TestData.NewSource(), "order.created", "ORDER.CREATED", "order.paid");

        Assert.Equal(ExpectedTypes, destination.EventTypes);
    }

    [Fact]
    public void RegisterRejectsTooManyEventTypes()
    {
        var types = Enumerable.Range(0, Destination.MaxEventTypes + 1).Select(i => $"type.{i}").ToArray();

        Assert.Throws<ArgumentException>(() => TestData.NewDestination(TestData.NewSource(), types));
    }

    [Theory]
    [InlineData("ftp://example.com/hooks")]
    [InlineData("https://user:pass@example.com/hooks")]
    [InlineData("/hooks")]
    public void RegisterRejectsInvalidUrl(string url)
    {
        var source = TestData.NewSource();

        Assert.Throws<ArgumentException>(() => Destination.Register(
            source.Id, "Orders API", new Uri(url, UriKind.RelativeOrAbsolute),
            TestData.NewSecret(), TestData.Settings, [], TestData.Now));
    }

    [Fact]
    public void RegisterRejectsMissingSource()
    {
        Assert.Throws<ArgumentException>(() => Destination.Register(
            Guid.Empty, "Orders API", new Uri("https://example.com/hooks"),
            TestData.NewSecret(), TestData.Settings, [], TestData.Now));
    }

    [Fact]
    public void RegisterRejectsMissingSecret()
    {
        var source = TestData.NewSource();

        Assert.Throws<ArgumentException>(() => Destination.Register(
            source.Id, "Orders API", new Uri("https://example.com/hooks"),
            secret: default, TestData.Settings, [], TestData.Now));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(61, 5)]
    [InlineData(10, 0)]
    [InlineData(10, 21)]
    public void DeliverySettingsRejectValuesOutOfRange(int timeoutSeconds, int maxAttempts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DeliverySettings(TimeSpan.FromSeconds(timeoutSeconds), maxAttempts));
    }
}
