using Hookivery.Domain.Deliveries;
using Hookivery.Domain.Destinations;
using Hookivery.Domain.Events;
using Hookivery.Domain.Sources;

using Microsoft.EntityFrameworkCore;

namespace Hookivery.Infrastructure.Persistence;

public sealed class HookiveryDbContext(DbContextOptions<HookiveryDbContext> options) : DbContext(options)
{
    public DbSet<Source> Sources => Set<Source>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<Destination> Destinations => Set<Destination>();

    public DbSet<Delivery> Deliveries => Set<Delivery>();

    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HookiveryDbContext).Assembly);
    }
}
