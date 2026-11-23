using Npgsql;

namespace Hookivery.IntegrationTests;

public sealed class PostgresConnectionTests
{
    [Fact]
    public async Task ConnectsToPostgresAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Hookivery")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Hookivery before running this test.");


        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);

        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync(CancellationToken.None);

        Assert.Equal(1, result);
    }
}
