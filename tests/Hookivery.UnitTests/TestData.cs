using Hookivery.Domain.Common;
using Hookivery.Domain.Deliveries;
using Hookivery.Domain.Destinations;
using Hookivery.Domain.Events;
using Hookivery.Domain.Sources;

namespace Hookivery.UnitTests;

internal static class TestData
{
    public const string OrderCreated = "order.created";

    public static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    public static readonly DeliverySettings Settings = new(TimeSpan.FromSeconds(10), maxAttempts: 5);

    public static SigningSecret NewSecret() =>
        SigningSecret.FromRawValue(new string('s', SigningSecret.MinLength));

    public static Source NewSource() => Source.Register("Billing", NewSecret(), Now);

    public static Destination NewDestination(Source source, params string[] eventTypes) =>
        Destination.Register(
            source.Id,
            "Orders API",
            new Uri("https://example.com/hooks"),
            NewSecret(),
            Settings,
            eventTypes,
            Now);

    public static Event NewEvent(Source source, string eventType = OrderCreated) =>
        Event.Receive(source, "evt-1", eventType, """{"id":1}""", Now);

    public static Delivery NewDelivery()
    {
        var source = NewSource();
        return Delivery.Create(NewEvent(source), NewDestination(source, OrderCreated), Now);
    }

    public static DeliveryAttempt Success(Delivery delivery) =>
        DeliveryAttempt.Record(
            delivery, Now, TimeSpan.FromMilliseconds(120), AttemptOutcome.Succeeded,
            statusCode: 200, error: null, responseBody: "ok");

    public static DeliveryAttempt HttpFailure(Delivery delivery, int statusCode = 500) =>
        DeliveryAttempt.Record(
            delivery, Now, TimeSpan.FromMilliseconds(120), AttemptOutcome.HttpError,
            statusCode, error: null, responseBody: null);

    public static DeliveryAttempt Timeout(Delivery delivery) =>
        DeliveryAttempt.Record(
            delivery, Now, TimeSpan.FromSeconds(10), AttemptOutcome.Timeout,
            statusCode: null, error: "The request timed out.", responseBody: null);
}
