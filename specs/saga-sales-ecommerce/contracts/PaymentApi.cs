// Payment Endpoints — Payment Service Contract

namespace SagaSales.Contracts;

public class CreatePaymentRequest
{
    public Guid OrderId { get; set; }
    public PaymentMethod Method { get; set; }
    public string? GatewayReference { get; set; }
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? TransactionReference { get; set; }
    public string? GatewayMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class RefundRequest
{
    public Guid PaymentId { get; set; }
    public decimal RefundAmount { get; set; }
}

public class RefundResultDto
{
    public Guid PaymentId { get; set; }
    public PaymentStatus NewStatus { get; set; }
    public string? GatewayRefundRef { get; set; }
}

// HTTP Endpoints
// POST   /api/payments           → CreatePayment (201 PaymentDto)
// POST   /api/payments/{id}/refund → RefundRequest → RefundResultDto
// GET    /api/payments/{orderId} → GetPaymentByOrder
// GET    /api/payments           → ListPayments (paged)

// Webhook Endpoint (Stripe-specific)
// POST   /webhooks/stripe        → Verifies signature, processes payment events
// Domain Events Published
// PaymentAuthorizedEvent  ← Stripe webhook or SDK call
// PaymentCapturedEvent
// PaymentFailedEvent
// PaymentRefundedEvent
