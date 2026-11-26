
namespace Hookivery.IntegrationTests.Persistence;

[CollectionDefinition(Name)]
public sealed class DatabaseCollectionDefinition : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
