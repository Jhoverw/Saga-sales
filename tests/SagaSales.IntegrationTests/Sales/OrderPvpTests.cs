using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// Spec 003 (FR-002, FR-003, FR-005, FR-008) against db/sales/V026: PVP lines, products without VAT,
/// and lines written with the previous rule. Requires <c>SAGASALES_TEST_SQL</c> on <c>saga_sales_test</c>.
/// </summary>
public class OrderPvpTests
{
    [Fact]
    public async Task Two_units_of_a_pvp_of_0_40_total_0_80()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Two_units_of_a_pvp_of_0_40_total_0_80)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = await NewTenantWithVatAsync(connection);

        var order = await CreateOrderAsync(connection, tenantId, Line("Coca-Cola", 0.40m, 2, taxable: true, pvp: true));
        var line = (await LinesAsync(connection, tenantId, order.Id)).Single();

        Assert.Equal(0.70m, order.Subtotal);
        Assert.Equal(0.10m, order.TaxAmount);
        Assert.Equal(0.80m, line.LineTotal);
        Assert.Equal(0.70m, line.LineNet);
        Assert.Equal(0.10m, line.LineTax);
        Assert.Equal(0.40m, line.UnitPriceWithTax);
        Assert.True(line.PriceIncludesTax);
    }

    [Fact]
    public async Task A_product_without_vat_carries_no_vat()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_product_without_vat_carries_no_vat)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = await NewTenantWithVatAsync(connection);

        var order = await CreateOrderAsync(connection, tenantId, Line("Pan", 1.00m, 1, taxable: false, pvp: true));

        Assert.Equal(1.00m, order.Subtotal);
        Assert.Equal(0m, order.TaxAmount);
    }

    [Fact]
    public async Task Lines_without_the_new_flags_keep_the_previous_rule()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Lines_without_the_new_flags_keep_the_previous_rule)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = await NewTenantWithVatAsync(connection);

        // No isTaxable / priceIncludesTax: what every caller sent before spec 003.
        var order = await CreateOrderAsync(connection, tenantId, new
        {
            productId = Guid.NewGuid(),
            productName = "Coca-Cola (precio sin IVA)",
            quantity = 2,
            unitPrice = 0.35m,
            priceSource = 1,
            stockAtPurchase = 9,
        });

        Assert.Equal(0.70m, order.Subtotal);
        Assert.Equal(0.11m, order.TaxAmount);
    }

    [Fact]
    public async Task A_mixed_order_adds_up_every_line()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_mixed_order_adds_up_every_line)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = await NewTenantWithVatAsync(connection);

        var order = await CreateOrderAsync(
            connection,
            tenantId,
            Line("Coca-Cola", 0.40m, 2, taxable: true, pvp: true),
            Line("Pan", 1.00m, 1, taxable: false, pvp: true),
            Line("Antiguo", 0.35m, 2, taxable: true, pvp: false));

        Assert.Equal(2.40m, order.Subtotal);
        Assert.Equal(0.21m, order.TaxAmount);
    }

    [Fact]
    public async Task Lines_stored_before_v026_are_read_with_the_previous_rule()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Lines_stored_before_v026_are_read_with_the_previous_rule)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = await NewTenantWithVatAsync(connection);
        var order = await CreateOrderAsync(connection, tenantId, Line("Antiguo", 0.35m, 2, taxable: true, pvp: false));

        // A line written before V026 has no stored amounts.
        await connection.ExecuteAsync(
            "UPDATE dbo.order_lines SET line_net = NULL, line_tax = NULL, line_total = NULL WHERE order_id = @Id AND tenant_id = @TenantId",
            new { order.Id, TenantId = tenantId });

        var line = (await LinesAsync(connection, tenantId, order.Id)).Single();

        Assert.Equal(0.70m, line.LineNet);
        Assert.Equal(0.11m, line.LineTax);
        Assert.Equal(0.81m, line.LineTotal);
        Assert.Equal(0.40m, line.UnitPriceWithTax);
        Assert.False(line.PriceIncludesTax);
    }

    private static object Line(string name, decimal price, int quantity, bool taxable, bool pvp) => new
    {
        productId = Guid.NewGuid(),
        productName = name,
        quantity,
        unitPrice = price,
        priceSource = 1,
        stockAtPurchase = 99,
        isTaxable = taxable,
        priceIncludesTax = pvp,
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
            "VALUES (@TenantId, 0.15, '2000-01-01', NULL, 'IVA PVP test');",
            new { TenantId = tenantId });
        return tenantId;
    }

    private static Task<OrderRow> CreateOrderAsync(SqlConnection connection, Guid tenantId, params object[] lines)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", tenantId);
        parameters.Add("customer_id", Guid.NewGuid());
        parameters.Add("shipping_address", "Mostrador");
        parameters.Add("created_by", Guid.NewGuid());
        parameters.Add("lines_json", JsonSerializer.Serialize(lines));
        parameters.Add("notes", null);

        return connection.QuerySingleAsync<OrderRow>(
            "dbo.sp_order_create", parameters, commandType: CommandType.StoredProcedure);
    }

    private static async Task<List<LineRow>> LinesAsync(SqlConnection connection, Guid tenantId, Guid orderId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", tenantId);
        parameters.Add("order_id", orderId);

        var rows = await connection.QueryAsync<LineRow>(
            "dbo.sp_order_lines", parameters, commandType: CommandType.StoredProcedure);
        return rows.AsList();
    }

    private sealed class OrderRow
    {
        public Guid Id { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }
    }

    private sealed class LineRow
    {
        public decimal UnitPrice { get; set; }

        public decimal UnitPriceWithTax { get; set; }

        public bool PriceIncludesTax { get; set; }

        public decimal LineNet { get; set; }

        public decimal LineTax { get; set; }

        public decimal LineTotal { get; set; }
    }
}
