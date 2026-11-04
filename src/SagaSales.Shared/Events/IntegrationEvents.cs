namespace SagaSales.Shared.Events;

// ── Catalog ─────────────────────────────────────────────────────────────────

public record CatalogProductCreatedEvent(
    Guid ProductId,
    Guid TenantId,
    string ProductName,
    decimal Price,
    string Currency,
    int StockQuantity) : IntegrationEvent;

public record CatalogProductUpdatedEvent(
    Guid ProductId,
    Guid TenantId,
    string ProductName,
    decimal NewPrice,
    int NewStockQuantity) : IntegrationEvent;

public record CatalogProductDeletedEvent(
    Guid ProductId,
    Guid TenantId) : IntegrationEvent;

// ── Sales ───────────────────────────────────────────────────────────────────

public record OrderPlacedEvent(
    Guid OrderId,
    Guid TenantId,
    Guid CustomerId,
    string CustomerEmail,
    IReadOnlyList<DTOs.OrderItemSnapshot> Items,
    decimal TotalAmount,
    DateTime OrderDate) : IntegrationEvent;

public record OrderStatusChangedEvent(
    Guid OrderId,
    Guid TenantId,
    string OldStatus,
    string NewStatus,
    DateTime StatusChangedAt) : IntegrationEvent;

// ── Payment ─────────────────────────────────────────────────────────────────

public record PaymentAuthorizedEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid TenantId,
    Enums.PaymentMethod Method,
    decimal Amount,
    DateTime AuthorizedAt) : IntegrationEvent;

public record PaymentCapturedEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid TenantId,
    DateTime CapturedAt) : IntegrationEvent;

public record PaymentRefundedEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid TenantId,
    decimal RefundAmount,
    DateTime RefundedAt) : IntegrationEvent;

// ── Customer ────────────────────────────────────────────────────────────────

public record CustomerCreatedEvent(
    Guid CustomerId,
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName) : IntegrationEvent;

// ── Notification ────────────────────────────────────────────────────────────

public record HighValueOrderAlertEvent(
    Guid OrderId,
    Guid TenantId,
    decimal Amount,
    DateTime OrderDate) : IntegrationEvent;

// ── Analytics ───────────────────────────────────────────────────────────────
