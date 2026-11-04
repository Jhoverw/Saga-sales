using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Platform;

/// <summary>
/// Proves the suite is wired to a real database. Every procedure test in this project is built the
/// same way: open a connection, call the procedure, assert the effect.
/// </summary>
public class TestDatabaseTests
{
    [Fact]
    public async Task Connection_opens_when_the_environment_is_configured()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Connection_opens_when_the_environment_is_configured)))
        {
            return;
        }

        await using var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();

        var version = await connection.ExecuteScalarAsync<int>("select 1");

        Assert.Equal(1, version);
    }
}
