namespace SagaSales.Payment.Domain;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>How the money was (or will be) received; required by <c>PaymentDto</c>.</summary>
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;

    public string? CardLast4 { get; set; }
    public decimal CashReceived { get; set; }
    public decimal ChangeGiven { get; set; }


    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public decimal RefundedAmount { get; set; }

    public string? TransactionReference { get; set; }

    public string? GatewayResponse { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public DateTime? CapturedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public decimal RefundableAmount => Amount - RefundedAmount;

    public void MarkAuthorized(string transactionReference, string? gatewayResponse)
    {
        TransactionReference = transactionReference;
        GatewayResponse = gatewayResponse;
        Status = PaymentStatus.Authorized;
        PaidAt = DateTime.UtcNow;
    }

    public void MarkCaptured()
    {
        if (Status is not (PaymentStatus.Authorized or PaymentStatus.PartiallyRefunded))
        {
            throw new ConflictException($"A payment in status {Status} cannot be captured.");
        }

        Status = PaymentStatus.Captured;
        CapturedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? reason)
    {
        Status = PaymentStatus.Failed;
        GatewayResponse = reason;
    }

    public void Refund(decimal amount)
    {
        Guard.AgainstNegativeOrZero(amount, nameof(amount));

        if (Status is not (PaymentStatus.Authorized or PaymentStatus.Captured or PaymentStatus.PartiallyRefunded))
        {
            throw new ConflictException($"A payment in status {Status} cannot be refunded.");
        }

        if (amount > RefundableAmount)
        {
            throw new ConflictException(
                $"Refund of {amount} exceeds the refundable amount of {RefundableAmount}.");
        }

        RefundedAmount += amount;
        RefundedAt = DateTime.UtcNow;
        Status = RefundedAmount >= Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
    }
}

/// <summary>
/// Local read-model of the order a payment belongs to, kept up to date from
/// <c>OrderPlacedEvent</c>. Payment no longer reads the Sales database directly.
/// </summary>
public class OrderReference
{
    public Guid OrderId { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public decimal Subtotal { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTime PlacedAt { get; set; }
}
