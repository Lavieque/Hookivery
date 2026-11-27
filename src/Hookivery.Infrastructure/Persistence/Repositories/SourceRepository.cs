using Hookivery.Application.Sources;
using Hookivery.Domain.Sources;

using Microsoft.EntityFrameworkCore;

namespace Hookivery.Infrastructure.Persistence.Repositories;

internal sealed class SourceRepository(HookiveryDbContext context) : ISourceRepository
{
    public async Task AddAsync(Source source, CancellationToken cancellationToken)
    {
        context.Sources.Add(source);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Source?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Sources.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Source>> ListAsync(int offset, int limit, CancellationToken cancellationToken) =>
        await context.Sources
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(Source source, CancellationToken cancellationToken)
    {
        if (context.Entry(source).State == EntityState.Detached)
        {
            context.Sources.Update(source);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
