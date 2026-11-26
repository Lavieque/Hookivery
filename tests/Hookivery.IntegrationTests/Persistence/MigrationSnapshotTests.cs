using Hookivery.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Hookivery.IntegrationTests.Persistence;

public sealed class MigrationSnapshotTests
{
    [Fact]
    public void ModelHasNoPendingChanges()
    {
        using var context = new HookiveryDbContext(
            new DbContextOptionsBuilder<HookiveryDbContext>()
                .UseNpgsql("Host=unused")
                .UseSnakeCaseNamingConvention()
                .Options);

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "The model differs from the last migration: run dotnet ef migrations add.");
    }
}
