using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// The order procedures: atomic creation of header and lines, the state machine, and payment
/// registration. Requires <c>scripts/apply-db.ps1 -Service sales</c> and <c>SAGASALES_TEST_SQL</c>.
/// </summary>
public class OrderProceduresTests
{
    private const short Pending = 1;
    private const short Processing = 3;
    private const short Shipped = 4;

    [Fact]
    public async Task Create_writes_the_header_and_every_line_in_one_go()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Create_writes_the_header_and_every_line_in_one_go)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        var lines = JsonSerializer.Serialize(new object[]
        {
            new { productId = Guid.NewGuid(), productName = "Widget", quantity = 2, unitPrice = 10.50m, priceSource = 1, stockAtPurchase = 5 },
            new { productId = Guid.NewGuid(), productName = "Gadget", quantity = 1, unitPrice = 4.00m, priceSource = 2, stockAtPurchase = 1 },
        });

        var order = await CreateOrderAsync(connection, tenantId, lines, "Calle 1, Quito");

        Assert.Equal(25.00m, order.Subtotal);   // 2 x 10.50 + 1 x 4.00
        Assert.Equal(3.75m, order.TaxAmount);   // V-03: 15 % in force on the order date
        Assert.Equal(Pending, order.Status);
        Assert.Equal(0m, order.PaidAmount);

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.order_lines WHERE order_id = @OrderId",
            new { OrderId = order.Id });

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Create_refuses_an_empty_shipping_address()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Create_refuses_an_empty_shipping_address)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        var lines = JsonSerializer.Serialize(new object[]
        {
            new { productId = Guid.NewGuid(), productName = "Widget", quantity = 1, unitPrice = 5m, priceSource = 1, stockAtPurchase = 1 },
        });

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            CreateOrderAsync(connection, tenantId, lines, "   "));

        Assert.Contains("VALIDATION_ERROR", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Change_status_refuses_skipping_a_step()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Change_status_refuses_skipping_a_step)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        var order = await CreateOrderAsync(connection, tenantId, OneLine(), "Calle 1, Quito");

        // pending → shipped is not allowed: it has to pass through confirmed and processing.
        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            connection.QuerySingleAsync<OrderRow>(
                "dbo.sp_order_change_status",
                new DynamicParameters()
                    .AddTenant(tenantId)
                    .AddOrder(order.Id)
                    .WithStatus(Shipped),
                commandType: CommandType.StoredProcedure));

        Assert.Contains("INVALID_TRANSITION", exception.Message, StringComparison.OrdinalIgnoreCase);

        // The row must be untouched after the refused transition.
        var stored = await connection.QuerySingleAsync<OrderRow>(
            "dbo.sp_order_get",
            new DynamicParameters().AddTenant(tenantId).AddOrder(order.Id),
            commandType: CommandType.StoredProcedure);

        Assert.Equal(Pending, stored.Status);
    }

    [Fact]
    public async Task Registering_the_full_amount_confirms_the_order()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Registering_the_full_amount_confirms_the_order)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        var order = await CreateOrderAsync(connection, tenantId, OneLine(), "Calle 1, Quito");

        var arguments = new DynamicParameters()
            .AddTenant(tenantId)
            .AddOrder(order.Id);
        // H-08 / V023: the balance is the total including VAT, so "fully paid" means paying the total.
        arguments.Add("amount", order.Subtotal + order.TaxAmount);

        var paid = await connection.QuerySingleAsync<OrderRow>(
            "dbo.sp_order_register_payment", arguments, commandType: CommandType.StoredProcedure);

        Assert.Equal(Processing, paid.Status);
        Assert.Equal(order.Subtotal + order.TaxAmount, paid.PaidAmount);
        Assert.NotNull(paid.PaidDate);
    }

    [Fact]
    public async Task Orders_are_invisible_to_another_tenant()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Orders_are_invisible_to_another_tenant)))
        {
            return;
        }

        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        var order = await CreateOrderAsync(connection, owner, OneLine(), "Calle 1, Quito");

        var stolen = await connection.QuerySingleOrDefaultAsync<OrderRow>(
            "dbo.sp_order_get",
            new DynamicParameters().AddTenant(stranger).AddOrder(order.Id),
            commandType: CommandType.StoredProcedure);

        Assert.Null(stolen);
    }

    private static string OneLine() => JsonSerializer.Serialize(new object[]
    {
        new { productId = Guid.NewGuid(), productName = "Widget", quantity = 1, unitPrice = 5m, priceSource = 1, stockAtPurchase = 1 },
    });

    [Fact]
    public async Task Create_is_refused_when_no_vat_rate_is_in_force()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Create_is_refused_when_no_vat_rate_is_in_force)))
        {
            return;
        }

        // A store that never configured VAT: the order must be refused, not stored with a silent 0 %.
        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        var arguments = new DynamicParameters()
            .AddTenant(tenantId)
            .WithAddress("Calle 1, Quito");
        arguments.Add("customer_id", Guid.NewGuid());
        arguments.Add("created_by", Guid.NewGuid());
        arguments.Add("lines_json", OneLine());
        arguments.Add("notes", null);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            connection.QuerySingleAsync<OrderRow>(
                "dbo.sp_order_create", arguments, commandType: CommandType.StoredProcedure));

        Assert.Contains("VALIDATION_ERROR", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VAT", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<OrderRow> CreateOrderAsync(
        SqlConnection connection,
        Guid tenantId,
        string linesJson,
        string shippingAddress)
    {
        // Every store has a 15 % VAT open since 2000 in these tests (V-03 needs a rate in force).
        await connection.ExecuteAsync(
            "IF NOT EXISTS (SELECT 1 FROM dbo.tax_rates WHERE tenant_id = @TenantId) " +
            "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) " +
            "VALUES (@TenantId, 0.15, '2000-01-01', NULL, 'IVA test');",
            new { TenantId = tenantId });

        var arguments = new DynamicParameters()
            .AddTenant(tenantId)
            .WithAddress(shippingAddress);
        arguments.Add("customer_id", Guid.NewGuid());
        arguments.Add("created_by", Guid.NewGuid());
        arguments.Add("lines_json", linesJson);
        arguments.Add("notes", null);

        return await connection.QuerySingleAsync<OrderRow>(
            "dbo.sp_order_create", arguments, commandType: CommandType.StoredProcedure);
    }

    private sealed class OrderRow
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public Guid CustomerId { get; set; }

        public short Status { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal PaidAmount { get; set; }

        public DateTime? PaidDate { get; set; }
    }
}

internal static class SalesProcedureArguments
{
    public static DynamicParameters AddTenant(this DynamicParameters parameters, Guid tenantId)
    {
        parameters.Add("p_tenant_id", tenantId);
        return parameters;
    }

    public static DynamicParameters AddOrder(this DynamicParameters parameters, Guid orderId)
    {
        parameters.Add("order_id", orderId);
        return parameters;
    }

    public static DynamicParameters WithStatus(this DynamicParameters parameters, short status)
    {
        parameters.Add("new_status", status);
        return parameters;
    }

    public static DynamicParameters WithAddress(this DynamicParameters parameters, string address)
    {
        parameters.Add("shipping_address", address);
        return parameters;
    }
}
