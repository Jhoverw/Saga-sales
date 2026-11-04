namespace SagaSales.Shared.DTOs;

public class PaymentDto
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public Enums.PaymentMethod Method { get; set; }

    public Enums.PaymentStatus Status { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string? TransactionReference { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? RefundedAt { get; set; }
}

public class CreatePaymentRequest
{
    public Guid OrderId { get; set; }

    public Enums.PaymentMethod Method { get; set; }

    public string? GatewayReference { get; set; }
}

public class RefundPaymentRequest
{
    public decimal RefundAmount { get; set; }
}
