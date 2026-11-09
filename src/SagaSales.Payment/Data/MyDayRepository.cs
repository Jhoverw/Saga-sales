namespace SagaSales.Payment.Data;

using SagaSales.BuildingBlocks.Data;

/// <summary>Cobros por forma de pago y caja chica (spec 006), vía <c>db/payment/V006</c>.</summary>
public interface IMyDayRepository
{
    Task<PaymentDaySummaryDto> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, Guid? userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PettyCashExpenseDto>> ListPettyCashAsync(DateTime fromUtc, DateTime toUtc, Guid? userId, CancellationToken cancellationToken);

    Task<PettyCashExpenseDto> CreatePettyCashAsync(
        CreatePettyCashExpenseRequest request,
        Guid userId,
        string? userName,
        CancellationToken cancellationToken);

    Task VoidPettyCashAsync(Guid id, Guid voidedBy, CancellationToken cancellationToken);

    Task<PettyCashSettingsDto> GetSettingsAsync(CancellationToken cancellationToken);

    Task<PettyCashSettingsDto> SetSettingsAsync(decimal maxAmount, Guid? updatedBy, CancellationToken cancellationToken);
}

public sealed class MyDayRepository(IStoredProcedureExecutor executor) : IMyDayRepository
{
    // Métodos guardados: 1 tarjeta · 4 transferencia · 5 efectivo · 9 depósito (y los de pasarela).
    private const short CardMethod = 1;
    private const short CashMethod = 5;

    public async Task<PaymentDaySummaryDto> GetSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("from_utc", fromUtc)
            .With("to_utc", toUtc)
            .With("created_by", userId);

        var rows = await executor.QueryProcAsync<MethodRow>(
            "dbo.sp_payment_summary_by_method", arguments, cancellationToken: cancellationToken);

        var expenses = await ListPettyCashAsync(fromUtc, toUtc, userId, cancellationToken);

        return BuildSummary(rows, expenses);
    }

    public async Task<IReadOnlyList<PettyCashExpenseDto>> ListPettyCashAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("from_utc", fromUtc)
            .With("to_utc", toUtc)
            .With("user_id", userId);

        var rows = await executor.QueryProcAsync<ExpenseRow>(
            "dbo.sp_petty_cash_list", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<PettyCashExpenseDto> CreatePettyCashAsync(
        CreatePettyCashExpenseRequest request,
        Guid userId,
        string? userName,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("category", (short)request.Category)
            .With("description", request.Description)
            .With("amount", request.Amount)
            .With("user_id", userId)
            .With("user_name", userName);

        // Sobre el máximo de la tienda → VALIDATION_ERROR → 400 con el máximo en el mensaje.
        var row = await executor.QueryProcSingleAsync<ExpenseRow>(
            "dbo.sp_petty_cash_create", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public Task VoidPettyCashAsync(Guid id, Guid voidedBy, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("id", id)
            .With("voided_by", voidedBy);

        return executor.ExecuteProcAsync("dbo.sp_petty_cash_void", arguments, cancellationToken: cancellationToken);
    }

    public async Task<PettyCashSettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var row = await executor.QueryProcSingleAsync<PettyCashSettingsDto>(
            "dbo.sp_petty_cash_settings_get", SqlArgs.New(), cancellationToken: cancellationToken);

        return row!;
    }

    public async Task<PettyCashSettingsDto> SetSettingsAsync(
        decimal maxAmount,
        Guid? updatedBy,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("max_amount", maxAmount)
            .With("updated_by", updatedBy);

        var row = await executor.QueryProcSingleAsync<PettyCashSettingsDto>(
            "dbo.sp_petty_cash_settings_set", arguments, cancellationToken: cancellationToken);

        return row!;
    }

    /// <summary>Agrupa los métodos en efectivo, tarjeta y otros, y descuenta la caja chica vigente.</summary>
    public static PaymentDaySummaryDto BuildSummary(
        IEnumerable<MethodRow> rows,
        IEnumerable<PettyCashExpenseDto> expenses)
    {
        var summary = new PaymentDaySummaryDto();

        foreach (var row in rows)
        {
            summary.Payments += row.Payments;

            switch (row.Method)
            {
                case CashMethod:
                    summary.Cash += row.Amount;
                    break;
                case CardMethod:
                case 2:
                    summary.Card += row.Amount;
                    break;
                default:
                    summary.Other += row.Amount;
                    break;
            }
        }

        summary.PettyCash = expenses.Where(e => !e.IsVoided).Sum(e => e.Amount);
        return summary;
    }

    public sealed class MethodRow
    {
        public short Method { get; set; }

        public int Payments { get; set; }

        public decimal Amount { get; set; }
    }

    private sealed class ExpenseRow
    {
        public Guid Id { get; set; }

        public short Category { get; set; }

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public bool IsVoided { get; set; }

        public PettyCashExpenseDto ToDto() => new()
        {
            Id = Id,
            Category = (PettyCashCategory)Category,
            Description = Description,
            Amount = Amount,
            UserId = UserId,
            UserName = UserName,
            CreatedAt = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            IsVoided = IsVoided,
        };
    }
}
