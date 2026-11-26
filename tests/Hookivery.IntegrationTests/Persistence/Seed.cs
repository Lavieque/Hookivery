using System.Security.Cryptography;

using Hookivery.Domain.Common;
using Hookivery.Domain.Destinations;
using Hookivery.Domain.Events;
using Hookivery.Domain.Sources;

namespace Hookivery.IntegrationTests.Persistence;

internal static class Seed
{
    public static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    public static SigningSecret NewSecret() =>
        SigningSecret.FromRawValue(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    public static Source NewSource() => Source.Register("orders-service", NewSecret(), Now);

    public static Destination NewDestination(Source source) =>
        Destination.Register(
            source.Id,
            "billing",
            new Uri("https://billing.example.com/webhooks"),
            NewSecret(),
            new DeliverySettings(TimeSpan.FromSeconds(30), maxAttempts: 5),
            ["order.created"],
            Now);

    public static Event NewEvent(Source source, string? externalId = null) =>
        Event.Receive(source, externalId ?? Guid.NewGuid().ToString(), "order.created", """{"orderId": 42}""", Now);
}
