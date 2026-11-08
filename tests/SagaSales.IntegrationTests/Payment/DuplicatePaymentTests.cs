using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Payment;

/// <summary>
/// H-08 / FR-016: several payments per order are allowed, but an identical payment (same order, method and
/// amount) within 10 seconds is a duplicate. Requires <c>SAGASALES_TEST_SQL</c> and V004/V005 on
/// <c>saga_payment_test</c>.
/// </summary>
public class DuplicatePaymentTests
{
    private const short Transfer = 4;
    private const short Cash = 5;

    [Fact]
    public async Task Different_payments_on_the_same_order_are_allowed()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Different_payments_on_the_same_order_are_allowed)))
        {
            return;
        }

        await using var connection = new SqlConnection(TestDatabase.ForService("payment"));
        await connection.OpenAsync();

        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await CreateAsync(connection, tenantId, orderId, 10m, Transfer);

        // Another method, same order: a legitimate second payment (H-08).
        await CreateAsync(connection, tenantId, orderId, 10m, Cash, cashReceived: 10m);

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.payments WHERE order_id = @OrderId",
            new { OrderId = orderId });

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task An_identical_payment_within_ten_seconds_is_refused()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(An_identical_payment_within_ten_seconds_is_refused)))
        {
            return;
        }

        await using var connection = new SqlConnection(TestDatabase.ForService("payment"));
        await connection.OpenAsync();

        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await CreateAsync(connection, tenantId, orderId, 5m, Transfer);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            CreateAsync(connection, tenantId, orderId, 5m, Transfer));

        Assert.Contains("PAYMENT_DUPLICATE", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CreateAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid orderId,
        decimal amount,
        short method,
        decimal? cashReceived = null)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", tenantId);
        parameters.Add("order_id", orderId);
        parameters.Add("customer_id", Guid.NewGuid());
        parameters.Add("amount", amount);
        parameters.Add("method", method);
        parameters.Add("reference", null);
        parameters.Add("paid_at", null);
        parameters.Add("cash_received", cashReceived);

        await connection.ExecuteAsync(
            "dbo.sp_payment_create_manual", parameters, commandType: CommandType.StoredProcedure);
    }
}
