using Hookivery.Domain.Sources;

namespace Hookivery.Application.Sources;

public interface ISourceRepository
{
    Task AddAsync(Source source, CancellationToken cancellationToken);

    Task<Source?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Source>> ListAsync(int offset, int limit, CancellationToken cancellationToken);

    Task SaveAsync(Source source, CancellationToken cancellationToken);
}
