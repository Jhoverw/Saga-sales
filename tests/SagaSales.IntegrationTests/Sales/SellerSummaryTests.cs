using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// Spec 006 (FR-001, FR-002, FR-005) contra db/sales/V027: ventas por día local, sin canceladas, por
/// vendedor. Requiere <c>SAGASALES_TEST_SQL</c> sobre <c>saga_sales_test</c>.
/// </summary>
public class SellerSummaryTests
{
    private const int Ecuador = -300;

    [Fact]
    public async Task A_sale_at_22h_local_belongs_to_that_local_day()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_sale_at_22h_local_belongs_to_that_local_day)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = await NewTenantWithVatAsync(connection);
        var seller = Guid.NewGuid();

        // 2026-11-09 22:00 en Ecuador = 2026-11-10 03:00 UTC.
        var order = await CreateOrderAsync(connection, tenantId, seller, Line(1.00m, 2));
        await SetDateAsync(connection, order, new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc));

        var days = await DailyAsync(connection, tenantId, seller,
            new DateTime(2026, 11, 9, 5, 0, 0), new DateTime(2026, 11, 11, 5, 0, 0));

        var day = Assert.Single(days);
        Assert.Equal(new DateTime(2026, 11, 9), day.Day);
        Assert.Equal(1, day.Orders);
        Assert.Equal(2.00m, day.Total);
    }

    [Fact]
    public async Task Cancelled_orders_and_other_sellers_do_not_count()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Cancelled_orders_and_other_sellers_do_not_count)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = await NewTenantWithVatAsync(connection);
        var seller = Guid.NewGuid();
        var other = Guid.NewGuid();

        var kept = await CreateOrderAsync(connection, tenantId, seller, Line(0.40m, 3));
        var cancelled = await CreateOrderAsync(connection, tenantId, seller, Line(5.00m, 1));
        await CreateOrderAsync(connection, tenantId, other, Line(9.00m, 1));
        await connection.ExecuteAsync("UPDATE dbo.orders SET status = 6 WHERE id = @cancelled", new { cancelled });

        var from = DateTime.UtcNow.AddHours(-1);
        var to = DateTime.UtcNow.AddHours(1);

        var mine = Assert.Single(await DailyAsync(connection, tenantId, seller, from, to));
        Assert.Equal(1, mine.Orders);
        Assert.Equal(1.20m, mine.Total);

        var store = Assert.Single(await DailyAsync(connection, tenantId, null, from, to));
        Assert.Equal(2, store.Orders);
        Assert.Equal(10.20m, store.Total);

        var products = (await connection.QueryAsync<ProductRow>(
            "dbo.sp_order_seller_products",
            new { p_tenant_id = tenantId, from_utc = from, to_utc = to, seller_id = seller },
            commandType: CommandType.StoredProcedure)).AsList();

        var product = Assert.Single(products);
        Assert.Equal(3, product.Quantity);
        Assert.Equal(1.20m, product.Total);
        Assert.NotEqual(Guid.Empty, kept);
    }

    private static object Line(decimal pvp, int quantity) => new
    {
        productId = Guid.NewGuid(),
        productName = "Producto panel",
        quantity,
        unitPrice = pvp,
        priceSource = 1,
        stockAtPurchase = 99,
        isTaxable = true,
        priceIncludesTax = true,
    };

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<Guid> NewTenantWithVatAsync(SqlConnection connection)
    {
        var tenantId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) " +
            "VALUES (@TenantId, 0.15, '2000-01-01', NULL, 'IVA panel test');",
            new { TenantId = tenantId });
        return tenantId;
    }

    private static async Task<Guid> CreateOrderAsync(SqlConnection connection, Guid tenantId, Guid seller, params object[] lines)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", tenantId);
        parameters.Add("customer_id", Guid.NewGuid());
        parameters.Add("shipping_address", "Mostrador");
        parameters.Add("created_by", seller);
        parameters.Add("lines_json", JsonSerializer.Serialize(lines));
        parameters.Add("notes", null);

        var row = await connection.QuerySingleAsync<IdRow>(
            "dbo.sp_order_create", parameters, commandType: CommandType.StoredProcedure);
        return row.Id;
    }

    private static Task SetDateAsync(SqlConnection connection, Guid orderId, DateTime utc) =>
        connection.ExecuteAsync("UPDATE dbo.orders SET order_date = @utc WHERE id = @orderId", new { utc, orderId });

    private static async Task<List<DayRow>> DailyAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid? seller,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var rows = await connection.QueryAsync<DayRow>(
            "dbo.sp_order_seller_daily",
            new { p_tenant_id = tenantId, from_utc = fromUtc, to_utc = toUtc, offset_minutes = Ecuador, seller_id = seller },
            commandType: CommandType.StoredProcedure);
        return rows.AsList();
    }

    private sealed class IdRow
    {
        public Guid Id { get; set; }
    }

    private sealed class DayRow
    {
        public DateTime Day { get; set; }

        public int Orders { get; set; }

        public decimal Total { get; set; }
    }

    private sealed class ProductRow
    {
        public int Quantity { get; set; }

        public decimal Total { get; set; }
    }
}
