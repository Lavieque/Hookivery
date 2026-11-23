using Microsoft.EntityFrameworkCore;

namespace Hookivery.Infrastructure.Persistence;

public sealed class HookiveryDbContext(DbContextOptions<HookiveryDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HookiveryDbContext).Assembly);
    }
}
