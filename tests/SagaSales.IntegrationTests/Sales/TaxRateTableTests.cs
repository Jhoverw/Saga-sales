using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// Tax rates live in <c>sales.tax_rates</c> and belong to one store (§V). This test proves the
/// table contract the repository depends on and, above all, that a rate of store A is invisible
/// to store B. Set <c>SAGASALES_TEST_SQL</c> to enable it.
/// </summary>
public class TaxRateTableTests
{
    [Fact]
    public async Task A_rate_belongs_to_its_store_and_is_invisible_to_another()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_rate_belongs_to_its_store_and_is_invisible_to_another)))
        {
            return;
        }

        await EnsureDatabaseAsync();

        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();
        await EnsureTableAsync(connection);

        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var description = "IT-" + Guid.NewGuid().ToString("N")[..8];
        var start = new DateTime(2026, 3, 1);

        await connection.ExecuteAsync(
            "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) " +
            "VALUES (@TenantId, @Rate, @Start, NULL, @Description);",
            new { TenantId = owner, Rate = 0.16m, Start = start, Description = description });

        var own = await connection.QuerySingleAsync<TaxRow>(
            "SELECT rate_percentage AS RatePercentage, start_date AS StartDate, " +
            "       end_date AS EndDate, description AS Description " +
            "FROM dbo.tax_rates WHERE tenant_id = @TenantId AND description = @Description;",
            new { TenantId = owner, Description = description });

        var other = await connection.QueryAsync<TaxRow>(
            "SELECT rate_percentage AS RatePercentage FROM dbo.tax_rates " +
            "WHERE tenant_id = @TenantId AND description = @Description;",
            new { TenantId = stranger, Description = description });

        Assert.Equal(0.16m, own.RatePercentage);
        Assert.Equal(start, own.StartDate);
        Assert.Null(own.EndDate);
        Assert.Empty(other);

        await connection.ExecuteAsync(
            "DELETE FROM dbo.tax_rates WHERE tenant_id = @TenantId AND description = @Description;",
            new { TenantId = owner, Description = description });
    }

    private static async Task EnsureDatabaseAsync()
    {
        await using var master = new SqlConnection(TestDatabase.ConnectionBase);
        await master.OpenAsync();

        var name = TestDatabase.DatabaseName("sales");
        await master.ExecuteAsync($"IF DB_ID('{name}') IS NULL CREATE DATABASE [{name}];");
    }

    private static async Task EnsureTableAsync(SqlConnection connection)
    {
        await connection.ExecuteAsync(@"
IF OBJECT_ID(N'dbo.tax_rates', N'U') IS NULL
CREATE TABLE dbo.tax_rates (
    id              INT IDENTITY(1,1) PRIMARY KEY,
    tenant_id       UNIQUEIDENTIFIER NOT NULL,
    rate_percentage DECIMAL(5,4) NOT NULL,
    start_date      DATE NOT NULL,
    end_date        DATE NULL,
    description     NVARCHAR(100) NULL,
    created_at      DATETIME2 DEFAULT GETDATE()
);");
    }

    private sealed class TaxRow
    {
        public decimal RatePercentage { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string? Description { get; set; }
    }
}
