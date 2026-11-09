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

    /// <summary>Cash only: what the customer handed over (H-09).</summary>
    public decimal? CashReceived { get; set; }

    /// <summary>Cash only: CashReceived − Amount (H-09).</summary>
    public decimal? ChangeGiven { get; set; }

    /// <summary>Comprobantes subidos (spec 007).</summary>
    public int AttachmentCount { get; set; }

    /// <summary>Tarjeta, transferencia o depósito: para enviar el pedido necesita comprobante (spec 007).</summary>
    public bool NeedsProof => (int)Method is 1 or 4 or 9;
}

/// <summary>Foto o PDF que respalda un pago (spec 007).</summary>
public class PaymentAttachmentDto
{
    public Guid Id { get; set; }

    public Guid PaymentId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public int SizeBytes { get; set; }

    public DateTime UploadedAt { get; set; }
}

/// <summary>Lo que Sales pregunta antes de enviar un pedido (spec 007, FR-003).</summary>
public class OrderProofStatusDto
{
    /// <summary>Pagos vigentes del pedido.</summary>
    public int Payments { get; set; }

    /// <summary>Pagos vigentes con tarjeta, transferencia o depósito sin comprobante.</summary>
    public int WithoutProof { get; set; }
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

    /// <summary>Card last four digits, transfer or deposit reference (optional).</summary>
    public string? Reference { get; set; }

    public DateTime? PaidAt { get; set; }

    /// <summary>Required for cash: the amount the customer handed over. Must be at least Amount (H-09).</summary>
    public decimal? CashReceived { get; set; }
}

public class RefundPaymentRequest
{
    public decimal RefundAmount { get; set; }
}
