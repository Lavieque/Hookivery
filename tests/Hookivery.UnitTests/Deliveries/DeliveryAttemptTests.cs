using Hookivery.Domain.Deliveries;

namespace Hookivery.UnitTests;

public sealed class DeliveryAttemptTests
{
    [Theory]
    [InlineData(AttemptOutcome.Succeeded, null, null)]
    [InlineData(AttemptOutcome.Succeeded, 500, null)]
    [InlineData(AttemptOutcome.HttpError, null, "refused")]
    [InlineData(AttemptOutcome.HttpError, 204, null)]
    [InlineData(AttemptOutcome.HttpError, 600, null)]
    [InlineData(AttemptOutcome.Timeout, 200, "timed out")]
    [InlineData(AttemptOutcome.Timeout, null, null)]
    [InlineData(AttemptOutcome.NetworkError, null, " ")]
    public void RecordRejectsInconsistentOutcome(AttemptOutcome outcome, int? statusCode, string? error)
    {
        var delivery = TestData.NewDelivery();

        Assert.Throws<ArgumentException>(() => DeliveryAttempt.Record(
            delivery, TestData.Now, TimeSpan.FromSeconds(1), outcome, statusCode, error, responseBody: null));
    }

    [Fact]
    public void RecordRejectsResponseBodyWithoutResponse()
    {
        var delivery = TestData.NewDelivery();

        Assert.Throws<ArgumentException>(() => DeliveryAttempt.Record(
            delivery, TestData.Now, TimeSpan.FromSeconds(1), AttemptOutcome.Timeout,
            statusCode: null, error: "timed out", responseBody: "partial"));
    }

    [Fact]
    public void RecordRejectsNegativeDuration()
    {
        var delivery = TestData.NewDelivery();

        Assert.Throws<ArgumentOutOfRangeException>(() => DeliveryAttempt.Record(
            delivery, TestData.Now, TimeSpan.FromMilliseconds(-1), AttemptOutcome.Succeeded,
            statusCode: 200, error: null, responseBody: null));
    }

    [Fact]
    public void RecordNumbersAfterPreviousAttempts()
    {
        var delivery = TestData.NewDelivery();
        delivery.ScheduleRetry(TestData.HttpFailure(delivery), TestData.Now.AddSeconds(30));

        var second = TestData.Success(delivery);

        Assert.Equal(2, second.AttemptNumber);
        Assert.Equal(delivery.Id, second.DeliveryId);
    }

    [Fact]
    public void RecordTruncatesErrorAndResponseBody()
    {
        var delivery = TestData.NewDelivery();

        var attempt = DeliveryAttempt.Record(
            delivery, TestData.Now, TimeSpan.FromSeconds(1), AttemptOutcome.HttpError,
            statusCode: 500, error: new string('e', 5000), responseBody: new string('b', 10_000));

        Assert.Equal(DeliveryAttempt.MaxErrorLength, attempt.Error!.Length);
        Assert.Equal(DeliveryAttempt.MaxResponseBodyLength, attempt.ResponseBody!.Length);
    }

    [Fact]
    public void FinishedAtIsStartPlusDuration()
    {
        var attempt = TestData.Timeout(TestData.NewDelivery());

        Assert.Equal(TestData.Now.AddSeconds(10), attempt.FinishedAt);
    }
}
