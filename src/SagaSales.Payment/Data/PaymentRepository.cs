namespace SagaSales.Payment.Data;

using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;

/// <summary>
/// Payments, through <c>db/payment/V002__functions.sql</c>. No ORM: this repository is the only way
/// into the payment database.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>Records money already received (transfer, cash, deposit) as a captured payment.</summary>
    Task<PaymentDto> CreateManualAsync(
        Guid orderId,
        Guid customerId,
        decimal amount,
        ManualPaymentMethod method,
        string? reference,
        DateTime? paidAt,
        decimal? cashReceived,
        CancellationToken cancellationToken,
        Guid? createdBy = null);

    /// <summary>Refuses an identical payment (same order, method and amount) recorded within 10 s (FR-016).</summary>
    Task AssertNotDuplicateAsync(Guid orderId, ManualPaymentMethod method, decimal amount, CancellationToken cancellationToken);

    Task<PaymentDto> RefundAsync(Guid paymentId, decimal amount, CancellationToken cancellationToken);

    Task<PaymentDto?> GetAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentDto>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    // ── spec 008: verificación ───────────────────────────────────────────────
    Task RequireVerificationAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<PaymentVerificationState?> GetVerificationStateAsync(Guid paymentId, CancellationToken cancellationToken);

    Task VerifyAsync(Guid paymentId, Guid verifiedBy, CancellationToken cancellationToken);

    Task RejectAsync(Guid paymentId, Guid rejectedBy, string reason, CancellationToken cancellationToken);

    Task UpsertOrderReferenceAsync(
        Guid orderId,
        Guid customerId,
        decimal subtotal,
        DateTime placedAt,
        CancellationToken cancellationToken);
}

public sealed class PaymentRepository(IStoredProcedureExecutor executor) : IPaymentRepository
{
    public async Task<PaymentDto> CreateManualAsync(
        Guid orderId,
        Guid customerId,
        decimal amount,
        ManualPaymentMethod method,
        string? reference,
        DateTime? paidAt,
        decimal? cashReceived,
        CancellationToken cancellationToken,
        Guid? createdBy = null)
    {
        var arguments = SqlArgs.New()
            .With("order_id", orderId)
            .With("customer_id", customerId)
            .With("amount", amount)
            .With("method", (short)method)
            .With("reference", string.IsNullOrWhiteSpace(reference) ? null : reference.Trim())
            .With("paid_at", paidAt)
            .With("cash_received", cashReceived)
            .With("created_by", createdBy);

        // A second live payment for the same order surfaces as PAYMENT_ALREADY_EXISTS → 409.
        var row = await executor.QueryProcSingleAsync<PaymentRow>(
            "dbo.sp_payment_create_manual", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task AssertNotDuplicateAsync(
        Guid orderId,
        ManualPaymentMethod method,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("order_id", orderId)
            .With("method", (short)method)
            .With("amount", amount);

        // PAYMENT_DUPLICATE surfaces as 409 before the order is touched.
        await executor.ExecuteProcAsync(
            "dbo.sp_payment_assert_not_duplicate", arguments, cancellationToken: cancellationToken);
    }

    public async Task<PaymentDto> RefundAsync(Guid paymentId, decimal amount, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("payment_id", paymentId)
            .With("amount", amount);

        // REFUND_EXCEEDS_REFUNDABLE surfaces as 409.
        var row = await executor.QueryProcSingleAsync<PaymentRow>(
            "dbo.sp_payment_refund", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task<PaymentDto?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("payment_id", paymentId);

        var row = await executor.QueryProcSingleAsync<PaymentRow>(
            "dbo.sp_payment_get", arguments, cancellationToken: cancellationToken);

        return row?.ToDto();
    }

    public async Task<IReadOnlyList<PaymentDto>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("order_id", orderId);

        var rows = await executor.QueryProcAsync<PaymentRow>(
            "dbo.sp_payment_list_by_order", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    public Task UpsertOrderReferenceAsync(
        Guid orderId,
        Guid customerId,
        decimal subtotal,
        DateTime placedAt,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("order_id", orderId)
            .With("customer_id", customerId)
            .With("subtotal", subtotal)
            .With("placed_at", placedAt);

        return executor.ExecuteProcAsync(
            "dbo.sp_order_reference_upsert", arguments, cancellationToken: cancellationToken);
    }

    public Task RequireVerificationAsync(Guid paymentId, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_payment_require_verification",
            SqlArgs.New().With("payment_id", paymentId),
            cancellationToken: cancellationToken);

    public Task<PaymentVerificationState?> GetVerificationStateAsync(Guid paymentId, CancellationToken cancellationToken)
        => executor.QueryProcSingleAsync<PaymentVerificationState>(
            "dbo.sp_payment_verification_get",
            SqlArgs.New().With("payment_id", paymentId),
            cancellationToken: cancellationToken);

    public Task VerifyAsync(Guid paymentId, Guid verifiedBy, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_payment_verify",
            SqlArgs.New().With("payment_id", paymentId).With("verified_by", verifiedBy),
            cancellationToken: cancellationToken);

    public Task RejectAsync(Guid paymentId, Guid rejectedBy, string reason, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_payment_reject",
            SqlArgs.New().With("payment_id", paymentId).With("rejected_by", rejectedBy).With("reason", reason),
            cancellationToken: cancellationToken);

    private sealed class PaymentRow
    {
        public Guid Id { get; set; }

        public Guid OrderId { get; set; }

        public Guid CustomerId { get; set; }

        public short Method { get; set; }

        public short Status { get; set; }

        public decimal Amount { get; set; }

        public decimal RefundedAmount { get; set; }

        public string Currency { get; set; } = "USD";

        public string? TransactionReference { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }

        public DateTime? CapturedAt { get; set; }

        public DateTime? RefundedAt { get; set; }

        public decimal? CashReceived { get; set; }

        public decimal? ChangeGiven { get; set; }

        public int AttachmentCount { get; set; }

        public short VerificationStatus { get; set; }

        public string? RejectionReason { get; set; }

        public PaymentDto ToDto() => new()
        {
            Id = Id,
            OrderId = OrderId,
            CustomerId = CustomerId,
            Method = (PaymentMethod)Method,
            Status = (PaymentStatus)Status,
            Amount = Amount,
            RefundedAmount = RefundedAmount,
            Currency = Currency,
            TransactionReference = TransactionReference,
            CreatedAt = CreatedAt,
            PaidAt = PaidAt,
            CapturedAt = CapturedAt,
            RefundedAt = RefundedAt,
            CashReceived = CashReceived,
            ChangeGiven = ChangeGiven,
            AttachmentCount = AttachmentCount,
            VerificationStatus = VerificationStatus,
            RejectionReason = RejectionReason,
        };
    }
}

/// <summary>Lo que el servicio revisa antes de verificar o rechazar un pago (spec 008).</summary>
public sealed class PaymentVerificationState
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public short VerificationStatus { get; set; }

    public short Status { get; set; }

    public int AttachmentCount { get; set; }
}
