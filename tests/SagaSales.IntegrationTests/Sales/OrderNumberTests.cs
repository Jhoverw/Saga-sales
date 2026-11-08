using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// V021: every order has a unique human-readable number PED-&lt;store&gt;-&lt;sequence&gt; assigned by
/// sp_order_create, and the sequence only moves forward. Requires <c>SAGASALES_TEST_SQL</c>.
/// </summary>
public class OrderNumberTests
{
    private static readonly Regex Format = new(@"^PED-[0-9A-F]{6}-\d{6}$", RegexOptions.CultureInvariant);

    [Fact]
    public async Task Each_order_gets_the_next_number_in_its_store_sequence()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Each_order_gets_the_next_number_in_its_store_sequence)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();
        await InsertRateAsync(connection, tenantId);

        var first = await CreateAsync(connection, tenantId);
        var second = await CreateAsync(connection, tenantId);

        Assert.Matches(Format, first);
        Assert.Matches(Format, second);
        Assert.EndsWith("-000001", first, StringComparison.Ordinal);
        Assert.EndsWith("-000002", second, StringComparison.Ordinal);
        Assert.Equal(first[..11], second[..11]); // same store prefix
    }

    [Fact]
    public async Task Numbers_are_never_repeated_across_stores()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Numbers_are_never_repeated_across_stores)))
        {
            return;
        }

        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();
        await InsertRateAsync(connection, storeA);
        await InsertRateAsync(connection, storeB);

        var numberA = await CreateAsync(connection, storeA);
        var numberB = await CreateAsync(connection, storeB);

        Assert.NotEqual(numberA, numberB);
    }

    private static async Task InsertRateAsync(SqlConnection connection, Guid tenantId)
    {
        await connection.ExecuteAsync(
            "IF NOT EXISTS (SELECT 1 FROM dbo.tax_rates WHERE tenant_id = @TenantId) " +
            "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) " +
            "VALUES (@TenantId, 0.15, '2000-01-01', NULL, 'IVA test');",
            new { TenantId = tenantId });
    }

    private static async Task<string> CreateAsync(SqlConnection connection, Guid tenantId)
    {
        var lines = JsonSerializer.Serialize(new object[]
        {
            new { productId = Guid.NewGuid(), productName = "Widget", quantity = 1, unitPrice = 5m, priceSource = 1, stockAtPurchase = 1 },
        });

        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", tenantId);
        parameters.Add("customer_id", Guid.NewGuid());
        parameters.Add("shipping_address", "Calle 1, Quito");
        parameters.Add("created_by", Guid.NewGuid());
        parameters.Add("lines_json", lines);
        parameters.Add("notes", null);

        var row = await connection.QuerySingleAsync<OrderNumberRow>(
            "dbo.sp_order_create", parameters, commandType: System.Data.CommandType.StoredProcedure);

        return row.OrderNumber;
    }

    private sealed class OrderNumberRow
    {
        public string OrderNumber { get; set; } = string.Empty;
    }
}
