// Sales Service — REST contract.
// Declarative contract: this file documents the API, it is not compiled.
// Procedures in data-model.md §5.

namespace SagaSales.Contracts;

// Order lifecycle (FR-013):
//   pending(1) → confirmed(2) → processing(3) → shipped(4) → delivered(5) → refunded(7)
//   pending/confirmed/processing → cancelled(6)
//   Any other transition → 409 INVALID_TRANSITION and the state does not change.

public class CreateOrderRequest
{
    /// <summary>The customer being served (D8). In the MVP the buyer does not sign in, so the
    /// staff user places the order on their behalf. Required.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Always required, whatever the product kind (FR-028).</summary>
    public string ShippingAddress { get; set; } = "";

    public string? Notes { get; set; }

    public IList<OrderItemRequest> Items { get; set; } = [];
}

public class OrderItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal Subtotal { get; set; }
    public decimal PaidAmount { get; set; }
    public string ShippingAddress { get; set; } = "";
    public IReadOnlyList<OrderLineDto> Items { get; set; } = [];
    public DateTime OrderDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public Guid CreatedBy { get; set; }
}

/// <summary>A line frozen at purchase time (FR-012). Later product edits never change it.</summary>
public class OrderLineDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public PriceSource PriceSource { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public class UpdateOrderStatusRequest
{
    public int NewStatus { get; set; }
}

// HTTP Endpoints — bearer token + tenant required.
//
// POST   /api/orders                  → 201 OrderDto
//        Reserves stock in Catalog for every line, snapshots name/price, creates the order and
//        publishes OrderPlacedEvent. If any step fails, every reservation already made is
//        released before returning (FR-011, FR-014).
//        409 INSUFFICIENT_STOCK · 400 VALIDATION_ERROR · 404 NOT_FOUND (product or customer)
// GET    /api/orders                  → 200 PagedResult<OrderDto>  ?pageNumber&pageSize&status
// GET    /api/orders/{id}             → 200 OrderDto · 404
// PUT    /api/orders/{id}/status      → 200 OrderDto · 409 INVALID_TRANSITION
//
// Invariants
//  - Stock never goes negative: the decrement happens inside Catalog's transaction (SC-003).
//  - Unit price is the one resolved for the buyer's customer type at confirmation (FR-007b).
//  - Subtotal equals the sum of the lines as captured, not a later recalculation (SC-006).
//  - A staff user of one store can never read or move another store's order (FR-005).

// Errors: 400 · 401 · 403 · 404 · 409 CONFLICT | INSUFFICIENT_STOCK | INVALID_TRANSITION

// Domain events published
//   OrderPlacedEvent · OrderStatusChangedEvent
