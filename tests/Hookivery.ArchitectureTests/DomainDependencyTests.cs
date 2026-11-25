using Hookivery.Domain.Sources;

namespace Hookivery.ArchitectureTests;

public sealed class DomainDependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Hookivery.Application",
        "Hookivery.Infrastructure",
        "Hookivery.Api",
        "Hookivery.Worker",
    ];

    [Fact]
    public void DomainReferencesNoPersistenceOrOuterLayer()
    {
        var violations = typeof(Source).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(violations);
    }
}
