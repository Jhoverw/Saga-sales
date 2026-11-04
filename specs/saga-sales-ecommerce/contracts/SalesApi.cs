// Order & Line Item Endpoints — Sales Service Contract

namespace SagaSales.Contracts;

public class CreateOrderRequest
{
    public IList<OrderItemDto> Items { get; set; } = [];
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
}

public class OrderItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string? ProductSnapshotName { get; set; }
    public decimal UnitPrice { get; set; }
    public int ProductSnapshotStockAtPurchase { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal Subtotal { get; set; }
    public IList<OrderItemDto> Items { get; set; } = [];
    public DateTime CreatedDate { get; set; }
    public DateTime PaidDate { get; set; }
    public decimal PaidAmount { get; set; }
    public CustomerSummaryDto? Customer { get; set; }
    public string TenantSlug { get; set; } = "";
}

public class UpdateOrderStatusRequest
{
    public OrderStatus NewStatus { get; set; }
}

// HTTP Endpoints
// POST   /api/orders               → CreateOrder (201, atomic stock deduction)
// GET    /api/orders                → ListOrders (paged, filter by status, customer)
// GET    /api/orders/{id}           → GetOrder by ID
// PUT    /api/orders/{id}/status    → UpdateOrderStatus (enforces transitions: Pending→Confirmed→Processing→Shipped)

// Domain Events Published
// OrderPlacedEvent  ← on order creation with stock deduction
// OrderStatusChangedEvent ← on status transition

// Critical Constraints
// - Stock deduction MUST be atomic; concurrent orders cannot oversell
// - If any item has insufficient stock → return 409 InsufficientStockException
// - Order total MUST equal sum(quantity × unitPrice) at creation time
// - Unit price must come from current catalog stock (never stale)
