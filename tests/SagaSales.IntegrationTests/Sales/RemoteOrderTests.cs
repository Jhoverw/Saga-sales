using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// Spec 008 contra db/sales/V031: un pedido se prepara y se envía solo con el pago verificado, vence si no se
/// verifica a tiempo, y contra entrega salta la verificación. Requiere <c>SAGASALES_TEST_SQL</c>.
/// </summary>
public class RemoteOrderTests
{
    [Fact]
    public async Task An_unverified_transfer_does_not_move_a_pedido_to_processing()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(An_unverified_transfer_does_not_move_a_pedido_to_processing)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, orderId, total) = await PedidoAsync(connection, cod: false);

        var paid = await RegisterAsync(connection, tenantId, orderId, total, verified: false);
        Assert.Equal((short)1, paid.Status);

        await ChangeStatusAsync(connection, tenantId, orderId, 2);
        var refused = await Assert.ThrowsAsync<SqlException>(() => ChangeStatusAsync(connection, tenantId, orderId, 3));
        Assert.Contains("verificado", refused.Message, StringComparison.Ordinal);

        var verified = await AdjustAsync(connection, "dbo.sp_order_payment_verified", tenantId, orderId, total);
        Assert.Equal((short)3, verified.Status);
    }

    [Fact]
    public async Task A_rejected_payment_stops_counting()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_rejected_payment_stops_counting)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, orderId, total) = await PedidoAsync(connection, cod: false);

        await RegisterAsync(connection, tenantId, orderId, total, verified: false);
        var rejected = await AdjustAsync(connection, "dbo.sp_order_payment_rejected", tenantId, orderId, total);

        Assert.Equal(0m, rejected.PaidAmount);
    }

    [Fact]
    public async Task Counter_sales_keep_advancing_when_paid()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Counter_sales_keep_advancing_when_paid)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, orderId, total) = await OrderAsync(connection);

        var paid = await RegisterAsync(connection, tenantId, orderId, total, verified: true);
        Assert.Equal((short)3, paid.Status);
    }

    [Fact]
    public async Task An_expired_hold_is_listed_and_a_cash_on_delivery_never_is()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(An_expired_hold_is_listed_and_a_cash_on_delivery_never_is)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, expiring, _) = await PedidoAsync(connection, cod: false);
        var (_, cod, _) = await PedidoAsync(connection, cod: true, tenantId);

        await connection.ExecuteAsync(
            "UPDATE dbo.orders SET hold_until = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE id IN (@expiring, @cod)",
            new { expiring, cod });

        var due = (await connection.QueryAsync<DueRow>(
            "dbo.sp_order_holds_due", new { max_rows = 1000 }, commandType: CommandType.StoredProcedure)).ToList();

        Assert.Contains(due, d => d.OrderId == expiring && d.TenantId == tenantId);
        Assert.DoesNotContain(due, d => d.OrderId == cod);
    }

    [Fact]
    public async Task Cash_on_delivery_ships_unpaid_but_is_delivered_only_once_paid()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Cash_on_delivery_ships_unpaid_but_is_delivered_only_once_paid)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, orderId, total) = await PedidoAsync(connection, cod: true);

        await ChangeStatusAsync(connection, tenantId, orderId, 2);
        await ChangeStatusAsync(connection, tenantId, orderId, 3);
        await connection.QueryAsync(
            "dbo.sp_order_ship",
            new { p_tenant_id = tenantId, order_id = orderId, shipping_method = (short)2, courier_company = (string?)null, tracking_number = (string?)null },
            commandType: CommandType.StoredProcedure);

        var refused = await Assert.ThrowsAsync<SqlException>(() => ChangeStatusAsync(connection, tenantId, orderId, 5));
        Assert.Contains("contra entrega", refused.Message, StringComparison.Ordinal);

        await RegisterAsync(connection, tenantId, orderId, total, verified: true);
        await ChangeStatusAsync(connection, tenantId, orderId, 5);
    }

    [Fact]
    public async Task The_hold_uses_the_store_setting()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_hold_uses_the_store_setting)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "dbo.sp_sales_settings_set", new { p_tenant_id = tenantId, order_hold_hours = 12 }, commandType: CommandType.StoredProcedure);

        var (_, orderId, _) = await PedidoAsync(connection, cod: false, tenantId);

        var hours = await connection.QuerySingleAsync<int>(
            "SELECT DATEDIFF(HOUR, order_date, hold_until) FROM dbo.orders WHERE id = @orderId", new { orderId });
        Assert.Equal(12, hours);
    }

    [Fact]
    public async Task Reserved_stock_counts_only_pedidos_that_have_not_left()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Reserved_stock_counts_only_pedidos_that_have_not_left)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, waiting, _) = await PedidoAsync(connection, cod: false);
        var (_, cancelled, _) = await PedidoAsync(connection, cod: false, tenantId);
        await OrderAsync(connection, tenantId);
        await ChangeStatusAsync(connection, tenantId, cancelled, 6);

        var reserved = (await connection.QueryAsync<ReservedRow>(
            "dbo.sp_order_reserved_stock", new { p_tenant_id = tenantId }, commandType: CommandType.StoredProcedure)).ToList();

        // Every test order has its own product with 2 units: only the waiting pedido reserves.
        var row = Assert.Single(reserved);
        Assert.Equal(2, row.Quantity);
        Assert.Equal(1, row.Orders);
        Assert.NotEqual(Guid.Empty, waiting);
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<(Guid TenantId, Guid OrderId, decimal Total)> OrderAsync(SqlConnection connection, Guid? tenant = null)
    {
        var tenantId = tenant ?? Guid.NewGuid();
        if (tenant is null)
        {
            await connection.ExecuteAsync(
                "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) VALUES (@tenantId, 0.15, '2000-01-01', NULL, 'IVA pedidos test');",
                new { tenantId });
        }

        var lines = JsonSerializer.Serialize(new[]
        {
            new { productId = Guid.NewGuid(), productName = "Producto en vivo", quantity = 2, unitPrice = 5m, priceSource = 1, stockAtPurchase = 9, isTaxable = true, priceIncludesTax = true },
        });

        var row = await connection.QuerySingleAsync<OrderRow>(
            "dbo.sp_order_create",
            new { p_tenant_id = tenantId, customer_id = Guid.NewGuid(), shipping_address = "Guayaquil", created_by = Guid.NewGuid(), lines_json = lines, notes = (string?)null },
            commandType: CommandType.StoredProcedure);
        return (tenantId, row.Id, row.Subtotal + row.TaxAmount);
    }

    private static async Task<(Guid TenantId, Guid OrderId, decimal Total)> PedidoAsync(SqlConnection connection, bool cod, Guid? tenant = null)
    {
        if (tenant is { } existing)
        {
            var hasVat = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.tax_rates WHERE tenant_id = @existing", new { existing });
            if (hasVat == 0)
            {
                await connection.ExecuteAsync(
                    "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) VALUES (@existing, 0.15, '2000-01-01', NULL, 'IVA pedidos test');",
                    new { existing });
            }
        }

        var order = await OrderAsync(connection, tenant);
        await connection.ExecuteAsync(
            "dbo.sp_order_set_channel",
            new { p_tenant_id = order.TenantId, order_id = order.OrderId, channel = (short)2, cash_on_delivery = cod },
            commandType: CommandType.StoredProcedure);
        return order;
    }

    private static Task<OrderRow> RegisterAsync(SqlConnection connection, Guid tenantId, Guid orderId, decimal amount, bool verified) =>
        connection.QuerySingleAsync<OrderRow>(
            "dbo.sp_order_register_payment",
            new { p_tenant_id = tenantId, order_id = orderId, amount, verified },
            commandType: CommandType.StoredProcedure);

    private static Task<OrderRow> AdjustAsync(SqlConnection connection, string procedure, Guid tenantId, Guid orderId, decimal amount) =>
        connection.QuerySingleAsync<OrderRow>(
            procedure, new { p_tenant_id = tenantId, order_id = orderId, amount }, commandType: CommandType.StoredProcedure);

    private static Task ChangeStatusAsync(SqlConnection connection, Guid tenantId, Guid orderId, short status) =>
        connection.QueryAsync(
            "dbo.sp_order_change_status",
            new { p_tenant_id = tenantId, order_id = orderId, new_status = status },
            commandType: CommandType.StoredProcedure);

    private sealed class OrderRow
    {
        public Guid Id { get; set; }

        public short Status { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal PaidAmount { get; set; }
    }

    private sealed class ReservedRow
    {
        public int Quantity { get; set; }

        public int Orders { get; set; }
    }

    private sealed class DueRow
    {
        public Guid TenantId { get; set; }

        public Guid OrderId { get; set; }
    }
}
