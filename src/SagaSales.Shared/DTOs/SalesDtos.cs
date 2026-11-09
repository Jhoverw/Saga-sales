namespace SagaSales.Shared.DTOs;

/// <summary>A line of an order, frozen at purchase time (FR-012).</summary>
public class OrderLineDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    /// <summary>
    /// Price per unit as charged: the PVP when <see cref="PriceIncludesTax"/>, otherwise the price
    /// without VAT (orders placed before spec 003).
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Whether the list price or the base price was applied (FR-007b).</summary>
    public Enums.PriceSource PriceSource { get; set; }

    public bool IsTaxable { get; set; } = true;

    public bool PriceIncludesTax { get; set; }

    /// <summary>What one unit cost the buyer, VAT included.</summary>
    public decimal UnitPriceWithTax { get; set; }

    /// <summary>Base without VAT, as stored by the server (spec 003, FR-008).</summary>
    public decimal LineNet { get; set; }

    public decimal LineTax { get; set; }

    /// <summary>Line total with VAT, as stored by the server.</summary>
    public decimal LineTotal { get; set; }
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
