using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Payment;

/// <summary>
/// Spec 008 contra db/payment/V010: verificar exige comprobante, rechazar exige motivo y deja el pago fallido,
/// y el efectivo nunca queda por verificar. Requiere <c>SAGASALES_TEST_SQL</c>.
/// </summary>
public class PaymentVerificationTests
{
    [Fact]
    public async Task Verifying_needs_a_receipt()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Verifying_needs_a_receipt)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = Guid.NewGuid();
        var payment = await PayAsync(connection, tenantId, 4, 30m, null);
        await RequireAsync(connection, tenantId, payment);

        var refused = await Assert.ThrowsAsync<SqlException>(() => VerifyAsync(connection, tenantId, payment));
        Assert.Contains("comprobante", refused.Message, StringComparison.Ordinal);

        await AttachAsync(connection, tenantId, payment);
        await VerifyAsync(connection, tenantId, payment);

        Assert.Equal((short)2, await StateAsync(connection, payment));
    }

    [Fact]
    public async Task Rejecting_fails_the_payment()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Rejecting_fails_the_payment)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = Guid.NewGuid();
        var payment = await PayAsync(connection, tenantId, 9, 30m, null);
        await RequireAsync(connection, tenantId, payment);

        await connection.ExecuteAsync(
            "dbo.sp_payment_reject",
            new { p_tenant_id = tenantId, payment_id = payment, rejected_by = Guid.NewGuid(), reason = "No llegó al banco" },
            commandType: CommandType.StoredProcedure);

        var row = await connection.QuerySingleAsync<(short Verification, short Status)>(
            "SELECT verification_status, status FROM dbo.payments WHERE id = @payment", new { payment });
        Assert.Equal((short)3, row.Verification);
        Assert.Equal((short)4, row.Status);
    }

    [Fact]
    public async Task Cash_is_never_pending_verification()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Cash_is_never_pending_verification)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = Guid.NewGuid();
        var payment = await PayAsync(connection, tenantId, 5, 10m, 10m);
        await RequireAsync(connection, tenantId, payment);

        Assert.Equal((short)0, await StateAsync(connection, payment));
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("payment"));
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<Guid> PayAsync(SqlConnection connection, Guid tenantId, short method, decimal amount, decimal? cash)
    {
        var row = await connection.QuerySingleAsync<IdRow>(
            "dbo.sp_payment_create_manual",
            new { p_tenant_id = tenantId, order_id = Guid.NewGuid(), customer_id = Guid.NewGuid(), amount, method, reference = (string?)null, paid_at = (DateTime?)null, cash_received = cash },
            commandType: CommandType.StoredProcedure);
        return row.Id;
    }

    private static Task RequireAsync(SqlConnection connection, Guid tenantId, Guid payment) =>
        connection.ExecuteAsync("dbo.sp_payment_require_verification", new { p_tenant_id = tenantId, payment_id = payment }, commandType: CommandType.StoredProcedure);

    private static Task VerifyAsync(SqlConnection connection, Guid tenantId, Guid payment) =>
        connection.ExecuteAsync("dbo.sp_payment_verify", new { p_tenant_id = tenantId, payment_id = payment, verified_by = Guid.NewGuid() }, commandType: CommandType.StoredProcedure);

    private static Task AttachAsync(SqlConnection connection, Guid tenantId, Guid payment) =>
        connection.QueryAsync(
            "dbo.sp_payment_attachment_add",
            new { p_tenant_id = tenantId, id = Guid.NewGuid(), payment_id = payment, file_name = "deposito.pdf", content_type = "application/pdf", size_bytes = 2048, storage_key = $"{tenantId:N}/{Guid.NewGuid():N}.pdf", uploaded_by = Guid.NewGuid() },
            commandType: CommandType.StoredProcedure);

    private static Task<short> StateAsync(SqlConnection connection, Guid payment) =>
        connection.QuerySingleAsync<short>("SELECT verification_status FROM dbo.payments WHERE id = @payment", new { payment });

    private sealed class IdRow
    {
        public Guid Id { get; set; }
    }
}
