using Hookivery.Domain.Deliveries;

namespace Hookivery.UnitTests.Deliveries;

public sealed class DeliveryTests
{
    private static readonly DateTimeOffset RetryAt = TestData.Now.AddSeconds(30);

    [Fact]
    public void CreateStartsPendingAndDueImmediately()
    {
        var delivery = TestData.NewDelivery();

        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(0, delivery.AttemptCount);
        Assert.Equal(TestData.Now, delivery.NextAttemptAt);
        Assert.False(delivery.IsTerminal);
    }

    [Fact]
    public void CreateRejectsDestinationOfAnotherSource()
    {
        var webhookEvent = TestData.NewEvent(TestData.NewSource());
        var foreign = TestData.NewDestination(TestData.NewSource(), TestData.OrderCreated);

        Assert.Throws<InvalidOperationException>(() => Delivery.Create(webhookEvent, foreign, TestData.Now));
    }

    [Fact]
    public void CreateRejectsDestinationNotSubscribed()
    {
        var source = TestData.NewSource();
        var webhookEvent = TestData.NewEvent(source);
        var destination = TestData.NewDestination(source, "order.cancelled");

        Assert.Throws<InvalidOperationException>(() => Delivery.Create(webhookEvent, destination, TestData.Now));
    }

    [Fact]
    public void CreateRejectsInactiveDestination()
    {
        var source = TestData.NewSource();
        var webhookEvent = TestData.NewEvent(source);
        var destination = TestData.NewDestination(source, TestData.OrderCreated);
        destination.Deactivate();

        Assert.Throws<InvalidOperationException>(() => Delivery.Create(webhookEvent, destination, TestData.Now));
    }

    [Fact]
    public void CreateRejectsEventAfterFanOut()
    {
        var source = TestData.NewSource();
        var webhookEvent = TestData.NewEvent(source);
        var destination = TestData.NewDestination(source, TestData.OrderCreated);
        webhookEvent.MarkDispatched();

        Assert.Throws<InvalidOperationException>(() => Delivery.Create(webhookEvent, destination, TestData.Now));
    }

    [Fact]
    public void SucceedCompletesDelivery()
    {
        var delivery = TestData.NewDelivery();
        delivery.ScheduleRetry(TestData.HttpFailure(delivery), RetryAt);
        var attempt = TestData.Success(delivery);

        delivery.Succeed(attempt);

        Assert.Equal(DeliveryStatus.Succeeded, delivery.Status);
        Assert.True(delivery.IsTerminal);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.Equal(attempt.FinishedAt, delivery.CompletedAt);
        Assert.Equal(200, delivery.LastStatusCode);
        Assert.Null(delivery.LastError);
    }

    [Fact]
    public void SucceedWithFailedAttemptLeavesDeliveryUnchanged()
    {
        var delivery = TestData.NewDelivery();
        var attempt = TestData.HttpFailure(delivery);

        Assert.Throws<InvalidOperationException>(() => delivery.Succeed(attempt));

        AssertUntouched(delivery);
    }

    [Fact]
    public void ScheduleRetryWithSuccessfulAttemptLeavesDeliveryUnchanged()
    {
        var delivery = TestData.NewDelivery();
        var attempt = TestData.Success(delivery);

        Assert.Throws<InvalidOperationException>(() => delivery.ScheduleRetry(attempt, RetryAt));

        AssertUntouched(delivery);
    }

    [Fact]
    public void ScheduleRetryBeforeAttemptEndedLeavesDeliveryUnchanged()
    {
        var delivery = TestData.NewDelivery();
        var attempt = TestData.Timeout(delivery);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => delivery.ScheduleRetry(attempt, TestData.Now.AddSeconds(-1)));

        AssertUntouched(delivery);
    }

    [Fact]
    public void ScheduleRetryKeepsPendingAndRecordsFailure()
    {
        var delivery = TestData.NewDelivery();

        delivery.ScheduleRetry(TestData.HttpFailure(delivery, statusCode: 503), RetryAt);

        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal(RetryAt, delivery.NextAttemptAt);
        Assert.Equal(TestData.Now, delivery.LastAttemptAt);
        Assert.Equal(503, delivery.LastStatusCode);
        Assert.Equal("HttpError (HTTP 503)", delivery.LastError);
    }

    [Fact]
    public void FailEndsDeliveryWithReason()
    {
        var delivery = TestData.NewDelivery();

        delivery.Fail(TestData.Timeout(delivery), "Max attempts reached");

        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.True(delivery.IsTerminal);
        Assert.Null(delivery.LastStatusCode);
        Assert.Equal("Max attempts reached: Timeout: The request timed out.", delivery.LastError);
    }

    [Fact]
    public void FailTruncatesLastError()
    {
        var delivery = TestData.NewDelivery();

        delivery.Fail(TestData.Timeout(delivery), new string('x', 3000));

        Assert.Equal(Delivery.MaxErrorLength, delivery.LastError!.Length);
    }

    [Fact]
    public void TerminalDeliveryRejectsMoreAttempts()
    {
        var delivery = TestData.NewDelivery();
        delivery.Succeed(TestData.Success(delivery));
        var late = TestData.HttpFailure(delivery);

        Assert.Throws<InvalidOperationException>(() => delivery.ScheduleRetry(late, RetryAt));
        Assert.Equal(DeliveryStatus.Succeeded, delivery.Status);
    }

    [Fact]
    public void RejectsAttemptRecordedTwice()
    {
        var delivery = TestData.NewDelivery();
        var attempt = TestData.HttpFailure(delivery);
        delivery.ScheduleRetry(attempt, RetryAt);

        Assert.Throws<ArgumentException>(() => delivery.ScheduleRetry(attempt, RetryAt));
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public void RejectsAttemptOfAnotherDelivery()
    {
        var delivery = TestData.NewDelivery();
        var other = TestData.NewDelivery();

        Assert.Throws<ArgumentException>(() => delivery.Succeed(TestData.Success(other)));

        AssertUntouched(delivery);
    }

    private static void AssertUntouched(Delivery delivery)
    {
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(0, delivery.AttemptCount);
        Assert.Equal(TestData.Now, delivery.NextAttemptAt);
        Assert.Null(delivery.LastAttemptAt);
        Assert.Null(delivery.LastError);
    }
}
