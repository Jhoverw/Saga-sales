namespace SagaSales.Shared.DTOs;

public class OrderItemDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}

public class OrderDto
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal PaidAmount { get; set; }

    public string ShippingAddress { get; set; } = string.Empty;

    public IReadOnlyList<OrderItemDto> Items { get; set; } = [];

    public DateTime OrderDate { get; set; }

    public DateTime? PaidDate { get; set; }
}

public class CreateOrderRequest
{
    public IList<OrderItemRequest> Items { get; set; } = [];

    public string? ShippingAddress { get; set; }

    public string? Notes { get; set; }
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
