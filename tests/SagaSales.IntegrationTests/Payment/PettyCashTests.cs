using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Payment;

/// <summary>
/// Spec 006 (FR-003, FR-004, FR-006, FR-007) contra db/payment/V006: tope de caja chica, anulación,
/// cobrador del pago y resumen por forma de pago. Requiere <c>SAGASALES_TEST_SQL</c>.
/// </summary>
public class PettyCashTests
{
    [Fact]
    public async Task Without_settings_the_limit_is_20()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Without_settings_the_limit_is_20)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = Guid.NewGuid();

        Assert.Equal(20.00m, await connection.QuerySingleAsync<decimal>(
            "dbo.sp_petty_cash_settings_get", new { p_tenant_id = tenantId }, commandType: CommandType.StoredProcedure));

        await CreateAsync(connection, tenantId, Guid.NewGuid(), 20.00m);

        var exception = await Assert.ThrowsAsync<SqlException>(() => CreateAsync(connection, tenantId, Guid.NewGuid(), 20.01m));
        Assert.Contains("VALIDATION_ERROR", exception.Message, StringComparison.Ordinal);
        Assert.Contains("20.00", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_owner_sets_the_limit()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_owner_sets_the_limit)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = Guid.NewGuid();

        await connection.ExecuteAsync(
            "dbo.sp_petty_cash_settings_set",
            new { p_tenant_id = tenantId, max_amount = 50.00m, updated_by = Guid.NewGuid() },
            commandType: CommandType.StoredProcedure);

        var row = await CreateAsync(connection, tenantId, Guid.NewGuid(), 45.50m);
        Assert.Equal(45.50m, row.Amount);
        Assert.False(row.IsVoided);
    }

    [Fact]
    public async Task Voiding_twice_is_a_conflict()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Voiding_twice_is_a_conflict)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = Guid.NewGuid();
        var row = await CreateAsync(connection, tenantId, Guid.NewGuid(), 3.00m);

        await VoidAsync(connection, tenantId, row.Id);
        var exception = await Assert.ThrowsAsync<SqlException>(() => VoidAsync(connection, tenantId, row.Id));
        Assert.Contains("CONFLICT", exception.Message, StringComparison.Ordinal);

        var other = await Assert.ThrowsAsync<SqlException>(() => VoidAsync(connection, Guid.NewGuid(), row.Id));
        Assert.Contains("NOT_FOUND", other.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_summary_splits_methods_by_cashier()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_summary_splits_methods_by_cashier)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var tenantId = Guid.NewGuid();
        var cashier = Guid.NewGuid();
        var other = Guid.NewGuid();

        await PayAsync(connection, tenantId, cashier, 5, 10.00m, cashReceived: 20.00m);
        await PayAsync(connection, tenantId, cashier, 1, 4.00m, cashReceived: null);
        await PayAsync(connection, tenantId, cashier, 4, 6.00m, cashReceived: null);
        await PayAsync(connection, tenantId, other, 5, 100.00m, cashReceived: 100.00m);

        var rows = (await connection.QueryAsync<MethodRow>(
            "dbo.sp_payment_summary_by_method",
            new { p_tenant_id = tenantId, from_utc = DateTime.UtcNow.AddHours(-1), to_utc = DateTime.UtcNow.AddHours(1), created_by = cashier },
            commandType: CommandType.StoredProcedure)).ToDictionary(r => r.Method);

        Assert.Equal(10.00m, rows[5].Amount);
        Assert.Equal(4.00m, rows[1].Amount);
        Assert.Equal(6.00m, rows[4].Amount);
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("payment"));
        await connection.OpenAsync();
        return connection;
    }

    private static Task<ExpenseRow> CreateAsync(SqlConnection connection, Guid tenantId, Guid userId, decimal amount) =>
        connection.QuerySingleAsync<ExpenseRow>(
            "dbo.sp_petty_cash_create",
            new
            {
                p_tenant_id = tenantId,
                category = (short)2,
                description = "Foco de la bodega",
                amount,
                user_id = userId,
                user_name = "Cajero prueba",
            },
            commandType: CommandType.StoredProcedure);

    private static Task VoidAsync(SqlConnection connection, Guid tenantId, Guid id) =>
        connection.ExecuteAsync(
            "dbo.sp_petty_cash_void",
            new { p_tenant_id = tenantId, id, voided_by = Guid.NewGuid() },
            commandType: CommandType.StoredProcedure);

    private static Task PayAsync(SqlConnection connection, Guid tenantId, Guid cashier, short method, decimal amount, decimal? cashReceived) =>
        connection.QuerySingleAsync(
            "dbo.sp_payment_create_manual",
            new
            {
                p_tenant_id = tenantId,
                order_id = Guid.NewGuid(),
                customer_id = Guid.NewGuid(),
                amount,
                method,
                reference = (string?)null,
                paid_at = (DateTime?)null,
                cash_received = cashReceived,
                created_by = cashier,
            },
            commandType: CommandType.StoredProcedure);

    private sealed class ExpenseRow
    {
        public Guid Id { get; set; }

        public decimal Amount { get; set; }

        public bool IsVoided { get; set; }
    }

    private sealed class MethodRow
    {
        public short Method { get; set; }

        public decimal Amount { get; set; }
    }
}
