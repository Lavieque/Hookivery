using Hookivery.Infrastructure.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hookivery.IntegrationTests.Sources;

public sealed class SourceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__Hookivery")
        ?? throw new InvalidOperationException("Set ConnectionStrings__Hookivery before running this test.");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Hookivery", _connectionString);

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<HookiveryDbContext>();
        await context.Database.MigrateAsync(CancellationToken.None);
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}
