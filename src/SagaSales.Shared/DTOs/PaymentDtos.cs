namespace SagaSales.Shared.DTOs;

public class PaymentDto
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public Enums.PaymentMethod Method { get; set; }

    public Enums.PaymentStatus Status { get; set; }

    public decimal Amount { get; set; }

    public decimal RefundedAmount { get; set; }

    public decimal RefundableAmount => Amount - RefundedAmount;

    public string Currency { get; set; } = "USD";

    public string? TransactionReference { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? CapturedAt { get; set; }

    public DateTime? RefundedAt { get; set; }
}

/// <summary>Online payment — v1 (E-04b). Not used in the MVP.</summary>
public class CreatePaymentRequest
{
    public Guid OrderId { get; set; }

    public Enums.PaymentMethod Method { get; set; }

    public string? GatewayReference { get; set; }
}

/// <summary>Payment collected outside the platform: transfer, deposit or cash (E-04a).</summary>
public class ManualPaymentRequest
{
    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public Enums.ManualPaymentMethod Method { get; set; } = Enums.ManualPaymentMethod.Transfer;

    public string? Reference { get; set; }

    public DateTime? PaidAt { get; set; }
}

public class RefundPaymentRequest
{
    public decimal RefundAmount { get; set; }
}
