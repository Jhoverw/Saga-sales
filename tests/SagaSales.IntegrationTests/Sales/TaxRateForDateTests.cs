using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// V-03: the VAT in force on a date is resolved by <c>sp_tax_rate_for_date</c>, and an order keeps the
/// rate it was created with. The rule lives in SQL (constitution Â§VIII), so it is tested against the
/// real procedure. Requires <c>scripts/apply-db.ps1 -UseContainer -DatabaseSuffix '_test'</c> and <c>SAGASALES_TEST_SQL</c>.
/// </summary>
public class TaxRateForDateTests
{
    [Fact]
    public async Task The_rate_in_force_follows_the_date_ranges()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_rate_in_force_follows_the_date_ranges)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        // Jan 2026: 8 %, from Feb 2026 on: 15 %, open-ended.
        await InsertRateAsync(connection, tenantId, 0.08m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Unspecified));
        await InsertRateAsync(connection, tenantId, 0.15m, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Unspecified), null);

        Assert.Null(await CurrentAsync(connection, tenantId, new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Unspecified)));
        Assert.Equal(0.08m, (await CurrentAsync(connection, tenantId, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)))!.RatePercentage);
        Assert.Equal(0.08m, (await CurrentAsync(connection, tenantId, new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Unspecified)))!.RatePercentage);
        Assert.Equal(0.15m, (await CurrentAsync(connection, tenantId, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Unspecified)))!.RatePercentage);
        Assert.Equal(0.15m, (await CurrentAsync(connection, tenantId, new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Unspecified)))!.RatePercentage);
    }

    [Fact]
    public async Task When_two_rates_overlap_the_most_recent_start_wins()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(When_two_rates_overlap_the_most_recent_start_wins)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        await InsertRateAsync(connection, tenantId, 0.15m, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Unspecified), null);
        await InsertRateAsync(connection, tenantId, 0.10m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(0.10m, (await CurrentAsync(connection, tenantId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Unspecified)))!.RatePercentage);
        Assert.Equal(0.15m, (await CurrentAsync(connection, tenantId, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified)))!.RatePercentage);
    }

    [Fact]
    public async Task A_future_rate_change_does_not_alter_an_order_already_created()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_future_rate_change_does_not_alter_an_order_already_created)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        await InsertRateAsync(connection, tenantId, 0.15m, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), null);

        var lines = JsonSerializer.Serialize(new object[]
        {
            new { productId = Guid.NewGuid(), productName = "Widget", quantity = 2, unitPrice = 10m, priceSource = 1, stockAtPurchase = 9 },
        });

        var order = await connection.QuerySingleAsync<OrderTaxRow>(
            "dbo.sp_order_create",
            Params(new Dictionary<string, object?>
            {
                ["p_tenant_id"] = tenantId,
                ["customer_id"] = Guid.NewGuid(),
                ["shipping_address"] = "Calle 1, Quito",
                ["created_by"] = Guid.NewGuid(),
                ["lines_json"] = lines,
                ["notes"] = null,
            }),
            commandType: CommandType.StoredProcedure);

        Assert.Equal(3m, order.TaxAmount); // 20 x 15 %

        // Government raises the rate for a date that is already in the future for the store.
        await InsertRateAsync(connection, tenantId, 0.19m, DateTime.UtcNow.Date.AddDays(1), null);

        var stored = await connection.QuerySingleAsync<OrderTaxRow>(
            "dbo.sp_order_get",
            Params(new Dictionary<string, object?> { ["p_tenant_id"] = tenantId, ["order_id"] = order.Id }),
            commandType: CommandType.StoredProcedure);

        Assert.Equal(3m, stored.TaxAmount);
    }

    private static DynamicParameters Params(Dictionary<string, object?> values)
    {
        var parameters = new DynamicParameters();
        foreach (var (name, value) in values)
        {
            parameters.Add(name, value);
        }

        return parameters;
    }

    private static async Task InsertRateAsync(SqlConnection connection, Guid tenantId, decimal rate, DateTime start, DateTime? end)
    {
        await connection.ExecuteAsync(
            "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) " +
            "VALUES (@TenantId, @Rate, @Start, @End, 'IT');",
            new { TenantId = tenantId, Rate = rate, Start = start.Date, End = end?.Date });
    }

    private static Task<TaxRateRow?> CurrentAsync(SqlConnection connection, Guid tenantId, DateTime date)
        => connection.QuerySingleOrDefaultAsync<TaxRateRow>(
            "dbo.sp_tax_rate_for_date",
            Params(new Dictionary<string, object?> { ["p_tenant_id"] = tenantId, ["rate_date"] = date.Date }),
            commandType: CommandType.StoredProcedure);

    private sealed class TaxRateRow
    {
        public decimal RatePercentage { get; set; }
    }

    private sealed class OrderTaxRow
    {
        public Guid Id { get; set; }

        public decimal TaxAmount { get; set; }
    }
}
