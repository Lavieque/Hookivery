using Hookivery.Domain.Deliveries;
using Hookivery.Domain.Events;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hookivery.IntegrationTests.Persistence;

[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PersistenceMappingTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task RoundTripsTheWholeGraphAsync()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken.None);

        var source = Seed.NewSource();
        var destination = Seed.NewDestination(source);
        var webhookEvent = Seed.NewEvent(source);
        var delivery = Delivery.Create(webhookEvent, destination, Seed.Now);
        var attempt = DeliveryAttempt.Record(
            delivery, Seed.Now, TimeSpan.FromMilliseconds(850), AttemptOutcome.HttpError, 503, "Service Unavailable", "busy");
        delivery.ScheduleRetry(attempt, Seed.Now.AddMinutes(1));

        context.AddRange(source, destination, webhookEvent, delivery, attempt);
        await context.SaveChangesAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var storedSource = await context.Sources.SingleAsync(s => s.Id == source.Id, CancellationToken.None);
        var storedDestination = await context.Destinations.SingleAsync(d => d.Id == destination.Id, CancellationToken.None);
        var storedEvent = await context.Events.SingleAsync(e => e.Id == webhookEvent.Id, CancellationToken.None);
        var storedDelivery = await context.Deliveries.SingleAsync(d => d.Id == delivery.Id, CancellationToken.None);
        var storedAttempt = await context.DeliveryAttempts.SingleAsync(a => a.Id == attempt.Id, CancellationToken.None);

        Assert.Equal(source.Secret, storedSource.Secret);
        Assert.Equal(destination.Url, storedDestination.Url);
        Assert.Equal(TimeSpan.FromSeconds(30), storedDestination.Timeout);
        Assert.Equal(["order.created"], storedDestination.EventTypes);
        Assert.True(storedDestination.SubscribesTo("order.created"));

        Assert.Equal(webhookEvent.Payload, storedEvent.Payload);
        Assert.Equal(EventStatus.Pending, storedEvent.Status);
        Assert.Equal(Seed.Now, storedEvent.ReceivedAt);

        Assert.Equal(DeliveryStatus.Pending, storedDelivery.Status);
        Assert.Equal(1, storedDelivery.AttemptCount);
        Assert.Equal(503, storedDelivery.LastStatusCode);
        Assert.Equal(Seed.Now.AddMinutes(1), storedDelivery.NextAttemptAt);

        Assert.Equal(AttemptOutcome.HttpError, storedAttempt.Outcome);
        Assert.Equal(TimeSpan.FromMilliseconds(850), storedAttempt.Duration);
    }

    [Fact]
    public async Task RejectsASecondDeliveryOfTheSameEventToTheSameDestinationAsync()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken.None);

        var source = Seed.NewSource();
        var destination = Seed.NewDestination(source);
        var webhookEvent = Seed.NewEvent(source);
        context.AddRange(source, destination, webhookEvent);
        await context.SaveChangesAsync(CancellationToken.None);

        // The domain cannot see other deliveries; the database is the final guard.
        context.AddRange(
            Delivery.Create(webhookEvent, destination, Seed.Now),
            Delivery.Create(webhookEvent, destination, Seed.Now));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken.None));
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ux_deliveries_event_id_destination_id", postgres.ConstraintName);
    }

    [Fact]
    public async Task RejectsADuplicateExternalIdFromTheSameSourceAsync()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken.None);

        var source = Seed.NewSource();
        context.AddRange(source, Seed.NewEvent(source, "evt-1"), Seed.NewEvent(source, "evt-1"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken.None));
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("ux_events_source_id_external_id", postgres.ConstraintName);
    }

    [Fact]
    public async Task RefusesToDeleteASourceThatHasEventsAsync()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken.None);

        var source = Seed.NewSource();
        context.AddRange(source, Seed.NewEvent(source));
        await context.SaveChangesAsync(CancellationToken.None);

        var postgres = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Sources.Where(s => s.Id == source.Id).ExecuteDeleteAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
    }

    [Fact]
    public async Task CheckConstraintStopsRawSqlThatBypassesTheDomainAsync()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken.None);

        var source = Seed.NewSource();
        var destination = Seed.NewDestination(source);
        context.AddRange(source, destination);
        await context.SaveChangesAsync(CancellationToken.None);

        var postgres = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlAsync(
                $"UPDATE destinations SET max_attempts = 0 WHERE id = {destination.Id}",
                CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
    }
}
