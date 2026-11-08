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

    /// <summary>Human-readable number PED-&lt;store&gt;-&lt;sequence&gt;, unique per store. Also the receipt QR content.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>The customer being served, not the staff user who placed the order (D8).</summary>
    public Guid CustomerId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    /// <summary>VAT applied on the order date (V-03). Stored with the order; later rate changes do not touch it.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Subtotal + TaxAmount.</summary>
    public decimal Total { get; set; }

    public decimal PaidAmount { get; set; }

    public string ShippingAddress { get; set; } = string.Empty;

    public IReadOnlyList<OrderLineDto> Items { get; set; } = [];

    public DateTime OrderDate { get; set; }

    public DateTime? PaidDate { get; set; }

    /// <summary>Staff user who created the order, kept for audit.</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>Seller's name as recorded when the order was created (H-06 receipt).</summary>
    public string CreatedByName { get; set; } = string.Empty;
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
