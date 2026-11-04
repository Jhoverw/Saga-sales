// Payment Service — REST contract.
// Declarative contract: this file documents the API, it is not compiled.
// Procedures in data-model.md §6.

namespace SagaSales.Contracts;

// Payment states:
//   pending(1) · authorised(2) · captured(3) · failed(4) · refunded(5) · partially_refunded(6)

/// <summary>Payment collected outside the platform: transfer, deposit or cash (E-04a).</summary>
public class ManualPaymentRequest
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public ManualPaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public DateTime? PaidAt { get; set; }
}

public enum ManualPaymentMethod
{
    Transfer = 4,
    Cash = 5,
    Deposit = 9,
}

/// <summary>Online payment — v1 (E-04b). Not in the MVP.</summary>
public class CreatePaymentRequest
{
    public Guid OrderId { get; set; }
    public PaymentMethod Method { get; set; }
    public string? GatewayReference { get; set; }
}

public class RefundRequest
{
    public decimal RefundAmount { get; set; }
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
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

// HTTP Endpoints — bearer token + tenant required.
//
// POST /api/payments/manual            → 201 PaymentDto   (E-04a, MVP)
//      Confirms the order and adds to its paid amount. No gateway involved.
//      409 PAYMENT_ALREADY_EXISTS if the order already has a live payment (FR-016).
//      400 VALIDATION_ERROR on amount ≤ 0.
// POST /api/payments                   → 201 PaymentDto   (v1, online)
// POST /api/payments/{id}/refund       → 200 PaymentDto · 409 REFUND_EXCEEDS_REFUNDABLE (FR-017)
// GET  /api/payments/{id}              → 200 PaymentDto · 404
// GET  /api/payments?orderId=          → 200 PaymentDto[]
//
// Webhook
// POST /api/webhooks/stripe            → 202 Accepted · 400 INVALID_SIGNATURE
//      Signature verification is mandatory before touching any payment (FR-019). A payload whose
//      authenticity cannot be established must leave every payment unchanged.
//
// Invariants
//  - One live payment per order (FR-016).
//  - Refunds never exceed what remains refundable (FR-017); the database also enforces
//    check (refunded_amount <= amount).
//  - Payment never reads the Sales database: the order data comes from OrderPlacedEvent into
//    its own order_references table (R4).

// Errors: 400 · 401 · 403 · 404 · 409 · 402 PAYMENT_FAILED

// Domain events consumed
//   OrderPlacedEvent → stores the order reference
// Domain events published
//   PaymentAuthorizedEvent · PaymentCapturedEvent · PaymentRefundedEvent
