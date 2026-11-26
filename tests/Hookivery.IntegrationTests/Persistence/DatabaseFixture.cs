using Hookivery.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Hookivery.IntegrationTests.Persistence;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__Hookivery")
        ?? throw new InvalidOperationException("Set ConnectionStrings__Hookivery before running this test.");

    public HookiveryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HookiveryDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

