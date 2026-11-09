using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// Spec 007 (FR-004, FR-005) contra db/sales/V028: el paso a Enviado registra cómo sale el pedido.
/// Requiere <c>SAGASALES_TEST_SQL</c> sobre <c>saga_sales_test</c>.
/// </summary>
public class OrderShipmentTests
{
    [Fact]
    public async Task Change_status_no_longer_ships()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Change_status_no_longer_ships)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, orderId) = await ProcessingOrderAsync(connection);

        var exception = await Assert.ThrowsAsync<SqlException>(() => ChangeStatusAsync(connection, tenantId, orderId, 4));
        Assert.Contains("VALIDATION_ERROR", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_courier_without_tracking_number_is_refused()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_courier_without_tracking_number_is_refused)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, orderId) = await ProcessingOrderAsync(connection);

        var exception = await Assert.ThrowsAsync<SqlException>(() => ShipAsync(connection, tenantId, orderId, 1, "Servientrega", null));
        Assert.Contains("guía", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_courier_shipment_stores_company_and_tracking()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_courier_shipment_stores_company_and_tracking)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, orderId) = await ProcessingOrderAsync(connection);

        await ShipAsync(connection, tenantId, orderId, 1, " Servientrega ", "SV-0099");
        var shipment = await ShipmentAsync(connection, tenantId, orderId);

        Assert.Equal((short)1, shipment.ShippingMethod);
        Assert.Equal("Servientrega", shipment.CourierCompany);
        Assert.Equal("SV-0099", shipment.TrackingNumber);
        Assert.NotNull(shipment.ShippedAt);
        Assert.Equal((short)4, await connection.QuerySingleAsync<short>("SELECT status FROM dbo.orders WHERE id = @orderId", new { orderId }));
    }

    [Fact]
    public async Task Own_delivery_needs_no_tracking_and_only_ships_from_processing()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Own_delivery_needs_no_tracking_and_only_ships_from_processing)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var (tenantId, orderId) = await ProcessingOrderAsync(connection);

        await ShipAsync(connection, tenantId, orderId, 2, "ignorado", "ignorado");
        var shipment = await ShipmentAsync(connection, tenantId, orderId);
        Assert.Equal((short)2, shipment.ShippingMethod);
        Assert.Null(shipment.TrackingNumber);

        // Ya enviado: no se envía otra vez.
        var again = await Assert.ThrowsAsync<SqlException>(() => ShipAsync(connection, tenantId, orderId, 2, null, null));
        Assert.Contains("INVALID_TRANSITION", again.Message, StringComparison.Ordinal);
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<(Guid TenantId, Guid OrderId)> ProcessingOrderAsync(SqlConnection connection)
    {
        var tenantId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) " +
            "VALUES (@tenantId, 0.15, '2000-01-01', NULL, 'IVA envío test');",
            new { tenantId });

        var lines = JsonSerializer.Serialize(new[]
        {
            new { productId = Guid.NewGuid(), productName = "Pedido", quantity = 1, unitPrice = 10m, priceSource = 1, stockAtPurchase = 5, isTaxable = true, priceIncludesTax = true },
        });

        var order = await connection.QuerySingleAsync<IdRow>(
            "dbo.sp_order_create",
            new { p_tenant_id = tenantId, customer_id = Guid.NewGuid(), shipping_address = "Quito", created_by = Guid.NewGuid(), lines_json = lines, notes = (string?)null },
            commandType: CommandType.StoredProcedure);
        var orderId = order.Id;

        await ChangeStatusAsync(connection, tenantId, orderId, 2);
        await ChangeStatusAsync(connection, tenantId, orderId, 3);
        return (tenantId, orderId);
    }

    private static Task ChangeStatusAsync(SqlConnection connection, Guid tenantId, Guid orderId, short status) =>
        connection.QueryAsync(
            "dbo.sp_order_change_status",
            new { p_tenant_id = tenantId, order_id = orderId, new_status = status },
            commandType: CommandType.StoredProcedure);

    private static Task ShipAsync(SqlConnection connection, Guid tenantId, Guid orderId, short method, string? company, string? tracking) =>
        connection.QueryAsync(
            "dbo.sp_order_ship",
            new { p_tenant_id = tenantId, order_id = orderId, shipping_method = method, courier_company = company, tracking_number = tracking },
            commandType: CommandType.StoredProcedure);

    private static Task<ShipmentRow> ShipmentAsync(SqlConnection connection, Guid tenantId, Guid orderId) =>
        connection.QuerySingleAsync<ShipmentRow>(
            "dbo.sp_order_shipment_get",
            new { p_tenant_id = tenantId, order_id = orderId },
            commandType: CommandType.StoredProcedure);

    private sealed class IdRow
    {
        public Guid Id { get; set; }
    }

    private sealed class ShipmentRow
    {
        public short? ShippingMethod { get; set; }

        public string? CourierCompany { get; set; }

        public string? TrackingNumber { get; set; }

        public DateTime? ShippedAt { get; set; }
    }
}
