using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// H-08 against the real procedures: the balance is the total (subtotal + VAT), partial payments add up,
/// and a payment beyond the balance is refused without changing the order. Requires <c>SAGASALES_TEST_SQL</c>
/// and V023 applied to <c>saga_sales_test</c>.
/// </summary>
public class OrderPaymentsBalanceTests
{
    private const short Pending = 1;
    private const short Processing = 3;

    [Fact]
    public async Task Partial_payments_add_up_until_the_total_including_vat()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Partial_payments_add_up_until_the_total_including_vat)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();
        await InsertRateAsync(connection, tenantId);

        var order = await CreateOrderAsync(connection, tenantId, 25m); // 25 + 15 % = 28.75
        Assert.Equal(28.75m, order.Total);

        var afterFirst = await RegisterAsync(connection, tenantId, order.Id, 10m);
        Assert.Equal(10m, afterFirst.PaidAmount);
        Assert.Equal(Pending, afterFirst.Status);

        var afterSecond = await RegisterAsync(connection, tenantId, order.Id, 18.75m);
        Assert.Equal(28.75m, afterSecond.PaidAmount);
        Assert.Equal(Processing, afterSecond.Status);
        Assert.NotNull(afterSecond.PaidDate);
    }

    [Fact]
    public async Task A_payment_beyond_the_balance_is_refused_and_the_order_is_untouched()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_payment_beyond_the_balance_is_refused_and_the_order_is_untouched)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();
        await InsertRateAsync(connection, tenantId);

        var order = await CreateOrderAsync(connection, tenantId, 25m);
        await RegisterAsync(connection, tenantId, order.Id, 10m);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            RegisterAsync(connection, tenantId, order.Id, 30m));

        Assert.Contains("PAYMENT_EXCEEDS_BALANCE", exception.Message, StringComparison.OrdinalIgnoreCase);

        var stored = await ReadAsync(connection, tenantId, order.Id);
        Assert.Equal(10m, stored.PaidAmount);
        Assert.Equal(Pending, stored.Status);
    }

    private static async Task InsertRateAsync(SqlConnection connection, Guid tenantId)
    {
        await connection.ExecuteAsync(
            "IF NOT EXISTS (SELECT 1 FROM dbo.tax_rates WHERE tenant_id = @TenantId) " +
            "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) " +
            "VALUES (@TenantId, 0.15, '2000-01-01', NULL, 'IVA test');",
            new { TenantId = tenantId });
    }

    private static async Task<OrderRow> CreateOrderAsync(SqlConnection connection, Guid tenantId, decimal subtotal)
    {
        var lines = JsonSerializer.Serialize(new object[]
        {
            new { productId = Guid.NewGuid(), productName = "Widget", quantity = 1, unitPrice = subtotal, priceSource = 1, stockAtPurchase = 9 },
        });

        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", tenantId);
        parameters.Add("customer_id", Guid.NewGuid());
        parameters.Add("shipping_address", "Calle 1, Quito");
        parameters.Add("created_by", Guid.NewGuid());
        parameters.Add("lines_json", lines);
        parameters.Add("notes", null);

        return await connection.QuerySingleAsync<OrderRow>(
            "dbo.sp_order_create", parameters, commandType: CommandType.StoredProcedure);
    }

    private static Task<OrderRow> RegisterAsync(SqlConnection connection, Guid tenantId, Guid orderId, decimal amount)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", tenantId);
        parameters.Add("order_id", orderId);
        parameters.Add("amount", amount);

        return connection.QuerySingleAsync<OrderRow>(
            "dbo.sp_order_register_payment", parameters, commandType: CommandType.StoredProcedure);
    }

    private static Task<OrderRow> ReadAsync(SqlConnection connection, Guid tenantId, Guid orderId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", tenantId);
        parameters.Add("order_id", orderId);

        return connection.QuerySingleAsync<OrderRow>(
            "dbo.sp_order_get", parameters, commandType: CommandType.StoredProcedure);
    }

    private sealed class OrderRow
    {
        public Guid Id { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal PaidAmount { get; set; }

        public short Status { get; set; }

        public DateTime? PaidDate { get; set; }

        public decimal Total => Subtotal + TaxAmount;
    }
}
