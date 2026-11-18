namespace Hookivery.Worker;

internal sealed partial class HeartbeatWorker(ILogger<HeartbeatWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LogRunning(logger, DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker running at: {Time}")]
    private static partial void LogRunning(ILogger logger, DateTimeOffset time);
}
