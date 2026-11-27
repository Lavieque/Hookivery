using Hookivery.Application.Sources;
using Hookivery.Infrastructure.Persistence;
using Hookivery.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hookivery.Infrastructure.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Hookivery")
            ?? throw new InvalidOperationException(
                "Connection string 'Hookivery' is not configured.");

        services.AddDbContext<HookiveryDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());
        services.AddScoped<ISourceRepository, SourceRepository>();

        return services;
    }
}
