namespace SagaSales.Shared.DTOs;

/// <summary>A line of an order, frozen at purchase time (FR-012).</summary>
public class OrderLineDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>Whether the list price or the base price was applied (FR-007b).</summary>
    public Enums.PriceSource PriceSource { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}

public class OrderDto
{
    public Guid Id { get; set; }

    /// <summary>The customer being served, not the staff user who placed the order (D8).</summary>
    public Guid CustomerId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal PaidAmount { get; set; }

    public string ShippingAddress { get; set; } = string.Empty;

    public IReadOnlyList<OrderLineDto> Items { get; set; } = [];

    public DateTime OrderDate { get; set; }

    public DateTime? PaidDate { get; set; }

    /// <summary>Staff user who created the order, kept for audit.</summary>
    public Guid CreatedBy { get; set; }
}

public class CreateOrderRequest
{
    public Guid CustomerId { get; set; }

    public string ShippingAddress { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public IList<OrderItemRequest> Items { get; set; } = [];
}

public class OrderItemRequest
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }
}

public class UpdateOrderStatusRequest
{
    public int NewStatus { get; set; }
}
