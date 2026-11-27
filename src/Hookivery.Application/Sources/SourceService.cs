using Hookivery.Domain.Sources;

using Microsoft.Extensions.Logging;

namespace Hookivery.Application.Sources;

public sealed partial class SourceService(ISourceRepository sources, TimeProvider timeProvider, ILogger<SourceService> logger)
{
    public const int MaxPageSize = 100;

    public async Task<Source> RegisterAsync(string name, CancellationToken cancellationToken)
    {
        var source = Source.Register(name, SigningSecretGenerator.Generate(), timeProvider.GetUtcNow());

        await sources.AddAsync(source, cancellationToken);

        LogRegistered(logger, source.Id);
        return source;
    }

    public Task<Source?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        sources.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Source>> ListAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxPageSize);

        return sources.ListAsync(offset, limit, cancellationToken);
    }

    public async Task<Source?> UpdateAsync(Guid id, string name, bool isActive, CancellationToken cancellationToken)
    {
        var source = await sources.GetByIdAsync(id, cancellationToken);
        if (source is null)
        {
            return null;
        }

        source.Rename(name);
        if (isActive)
        {
            source.Activate();
        }
        else
        {
            source.Deactivate();
        }

        await sources.SaveAsync(source, cancellationToken);

        LogUpdated(logger, source.Id, source.IsActive);
        return source;
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await sources.GetByIdAsync(id, cancellationToken);
        if (source is null)
        {
            return false;
        }

        if (source.IsActive)
        {
            source.Deactivate();
            await sources.SaveAsync(source, cancellationToken);
            LogDeactivated(logger, source.Id);
        }

        return true;
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Source {SourceId} registered")]
    private static partial void LogRegistered(ILogger logger, Guid sourceId);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Source {SourceId} updated (active: {IsActive})")]
    private static partial void LogUpdated(ILogger logger, Guid sourceId, bool isActive);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Source {SourceId} deactivated")]
    private static partial void LogDeactivated(ILogger logger, Guid sourceId);
}