using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Payment;

/// <summary>
/// H-09 and H-10 against the real procedure: cash records what was received and the change, refuses
/// a short payment, and card records only a reference. Requires <c>SAGASALES_TEST_SQL</c> and
/// <c>apply-db.ps1 -DatabaseSuffix '_test'</c> with V003.
/// </summary>
public class CashChangeTests
{
    private const short Card = 1;
    private const short Cash = 5;

    [Fact]
    public async Task Cash_records_the_amount_received_and_the_change()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Cash_records_the_amount_received_and_the_change)))
        {
            return;
        }

        await using var connection = new SqlConnection(TestDatabase.ForService("payment"));
        await connection.OpenAsync();

        var row = await CreateAsync(connection, Guid.NewGuid(), 15m, Cash, reference: null, cashReceived: 20m);

        Assert.Equal(15m, row.Amount);
        Assert.Equal(20m, row.CashReceived);
        Assert.Equal(5m, row.ChangeGiven);
    }

    [Fact]
    public async Task Cash_is_refused_when_the_amount_received_does_not_cover_the_total()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Cash_is_refused_when_the_amount_received_does_not_cover_the_total)))
        {
            return;
        }

        await using var connection = new SqlConnection(TestDatabase.ForService("payment"));
        await connection.OpenAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            CreateAsync(connection, Guid.NewGuid(), 15m, Cash, reference: null, cashReceived: 10m));

        Assert.Contains("VALIDATION_ERROR", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cash_without_an_amount_received_is_refused()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Cash_without_an_amount_received_is_refused)))
        {
            return;
        }

        await using var connection = new SqlConnection(TestDatabase.ForService("payment"));
        await connection.OpenAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            CreateAsync(connection, Guid.NewGuid(), 15m, Cash, reference: null, cashReceived: null));

        Assert.Contains("VALIDATION_ERROR", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Card_records_the_method_and_reference_without_change()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Card_records_the_method_and_reference_without_change)))
        {
            return;
        }

        await using var connection = new SqlConnection(TestDatabase.ForService("payment"));
        await connection.OpenAsync();

        var row = await CreateAsync(connection, Guid.NewGuid(), 42m, Card, reference: "4242", cashReceived: null);

        Assert.Equal(Card, row.Method);
        Assert.Equal("4242", row.TransactionReference);
        Assert.Null(row.CashReceived);
        Assert.Null(row.ChangeGiven);
    }

    private static async Task<PaymentRow> CreateAsync(
        SqlConnection connection,
        Guid orderId,
        decimal amount,
        short method,
        string? reference,
        decimal? cashReceived)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", Guid.NewGuid());
        parameters.Add("order_id", orderId);
        parameters.Add("customer_id", Guid.NewGuid());
        parameters.Add("amount", amount);
        parameters.Add("method", method);
        parameters.Add("reference", reference);
        parameters.Add("paid_at", null);
        parameters.Add("cash_received", cashReceived);

        return await connection.QuerySingleAsync<PaymentRow>(
            "dbo.sp_payment_create_manual", parameters, commandType: CommandType.StoredProcedure);
    }

    private sealed class PaymentRow
    {
        public decimal Amount { get; set; }

        public short Method { get; set; }

        public string? TransactionReference { get; set; }

        public decimal? CashReceived { get; set; }

        public decimal? ChangeGiven { get; set; }
    }
}
