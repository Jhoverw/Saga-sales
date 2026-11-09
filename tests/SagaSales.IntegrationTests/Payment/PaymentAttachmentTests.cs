using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Payment;

/// <summary>
/// Spec 007 (FR-001, FR-003) contra db/payment/V007: comprobantes por pago y estado de respaldo del pedido.
/// Requiere <c>SAGASALES_TEST_SQL</c>.
/// </summary>
public class PaymentAttachmentTests
{
    [Fact]
    public async Task A_transfer_needs_a_receipt_and_cash_does_not()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_transfer_needs_a_receipt_and_cash_does_not)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        Assert.Equal(0, (await StatusAsync(connection, tenantId, orderId)).Payments);

        await PayAsync(connection, tenantId, orderId, 5, 10m, 10m);
        var transfer = await PayAsync(connection, tenantId, orderId, 4, 20m, null);

        var before = await StatusAsync(connection, tenantId, orderId);
        Assert.Equal(2, before.Payments);
        Assert.Equal(1, before.WithoutProof);

        await AttachAsync(connection, tenantId, transfer);

        var after = await StatusAsync(connection, tenantId, orderId);
        Assert.Equal(0, after.WithoutProof);
    }

    [Fact]
    public async Task A_receipt_cannot_be_attached_to_another_stores_payment()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_receipt_cannot_be_attached_to_another_stores_payment)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var payment = await PayAsync(connection, Guid.NewGuid(), Guid.NewGuid(), 4, 20m, null);

        var exception = await Assert.ThrowsAsync<SqlException>(() => AttachAsync(connection, Guid.NewGuid(), payment));
        Assert.Contains("NOT_FOUND", exception.Message, StringComparison.Ordinal);
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("payment"));
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<Guid> PayAsync(SqlConnection connection, Guid tenantId, Guid orderId, short method, decimal amount, decimal? cash)
    {
        var row = await connection.QuerySingleAsync<IdRow>(
            "dbo.sp_payment_create_manual",
            new
            {
                p_tenant_id = tenantId,
                order_id = orderId,
                customer_id = Guid.NewGuid(),
                amount,
                method,
                reference = (string?)null,
                paid_at = (DateTime?)null,
                cash_received = cash,
            },
            commandType: CommandType.StoredProcedure);
        return row.Id;
    }

    private static Task AttachAsync(SqlConnection connection, Guid tenantId, Guid paymentId) =>
        connection.QueryAsync(
            "dbo.sp_payment_attachment_add",
            new
            {
                p_tenant_id = tenantId,
                id = Guid.NewGuid(),
                payment_id = paymentId,
                file_name = "transferencia.jpg",
                content_type = "image/jpeg",
                size_bytes = 1024,
                storage_key = $"{tenantId:N}/{Guid.NewGuid():N}.jpg",
                uploaded_by = Guid.NewGuid(),
            },
            commandType: CommandType.StoredProcedure);

    private static Task<StatusRow> StatusAsync(SqlConnection connection, Guid tenantId, Guid orderId) =>
        connection.QuerySingleAsync<StatusRow>(
            "dbo.sp_order_payment_proof_status",
            new { p_tenant_id = tenantId, order_id = orderId },
            commandType: CommandType.StoredProcedure);

    private sealed class IdRow
    {
        public Guid Id { get; set; }
    }

    private sealed class StatusRow
    {
        public int Payments { get; set; }

        public int? WithoutProof { get; set; }
    }
}
