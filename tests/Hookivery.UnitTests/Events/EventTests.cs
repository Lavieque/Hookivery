using Hookivery.Domain.Events;

namespace Hookivery.UnitTests;

public sealed class EventTests
{
    [Fact]
    public void ReceiveNormalizesEventTypeAndTrimsExternalId()
    {
        var source = TestData.NewSource();

        var webhookEvent = Event.Receive(source, "  evt-42 ", " Order.Created ", "{}", TestData.Now);

        Assert.Equal("evt-42", webhookEvent.ExternalId);
        Assert.Equal("order.created", webhookEvent.EventType);
        Assert.Equal(source.Id, webhookEvent.SourceId);
        Assert.Equal(TestData.Now, webhookEvent.ReceivedAt);
        Assert.Equal(EventStatus.Pending, webhookEvent.Status);
    }

    [Fact]
    public void ReceiveRejectsMissingSource()
    {
        Assert.Throws<ArgumentNullException>(
            () => Event.Receive(null!, "evt-1", TestData.OrderCreated, "{}", TestData.Now));
    }

    [Fact]
    public void ReceiveRejectsInactiveSource()
    {
        var source = TestData.NewSource();
        source.Deactivate();

        Assert.Throws<InvalidOperationException>(
            () => Event.Receive(source, "evt-1", TestData.OrderCreated, "{}", TestData.Now));
    }

    [Fact]
    public void ReceiveRejectsTooLongExternalId()
    {
        var source = TestData.NewSource();
        var externalId = new string('x', Event.MaxExternalIdLength + 1);

        Assert.Throws<ArgumentException>(
            () => Event.Receive(source, externalId, TestData.OrderCreated, "{}", TestData.Now));
    }

    [Fact]
    public void DispatchedEventCannotChangeFanOutAgain()
    {
        var webhookEvent = TestData.NewEvent(TestData.NewSource());
        webhookEvent.MarkDispatched();

        Assert.Throws<InvalidOperationException>(webhookEvent.MarkNoDestinations);
        Assert.Equal(EventStatus.Dispatched, webhookEvent.Status);
    }

    [Fact]
    public void NoDestinationsIsTerminal()
    {
        var webhookEvent = TestData.NewEvent(TestData.NewSource());
        webhookEvent.MarkNoDestinations();

        Assert.Throws<InvalidOperationException>(webhookEvent.MarkDispatched);
        Assert.Equal(EventStatus.NoDestinations, webhookEvent.Status);
    }
}
