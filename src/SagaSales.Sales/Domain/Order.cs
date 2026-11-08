namespace SagaSales.Sales.Domain;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable number PED-&lt;store&gt;-&lt;sequence&gt;, unique per store (V021).</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal Subtotal { get; set; }

    /// <summary>VAT in force on the order date, computed by sp_order_create (V-03). Never recalculated.</summary>
    public decimal TaxAmount { get; set; }

    public decimal Total => Subtotal + TaxAmount;

    public decimal PaidAmount { get; set; }

    public string ShippingAddress { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    /// <summary>Renamed from the previous <c>CreatedDate</c> mismatch: queries and the
    /// model used two different names, so neither compiled.</summary>
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public DateTime? PaidDate { get; set; }

    public Guid CreatedBy { get; set; }

    /// <summary>Seller's name, copied at creation (V022) so the receipt does not depend on identity.</summary>
    public string CreatedByName { get; set; } = string.Empty;

    public IList<OrderItem> Items { get; set; } = [];

    public decimal TotalQuantity => Items.Sum(i => (decimal)i.Quantity);

    public void ChangeStatus(OrderStatus newStatus)
    {
        if (!OrderStatusTransitions.IsAllowed(Status, newStatus))
        {
            throw new ConflictException($"Cannot move order from {Status} to {newStatus}.");
        }

        Status = newStatus;

        if (newStatus == OrderStatus.Processing && PaidDate is null)
        {
            PaidDate = DateTime.UtcNow;
        }
    }

    public void RegisterPayment(decimal amount)
    {
        Guard.AgainstNegative(amount, nameof(amount));
        PaidAmount += amount;

        if (Status == OrderStatus.Pending)
        {
            ChangeStatus(OrderStatus.Confirmed);
        }
    }
}

/// <summary>
/// Legal order status transitions. Previously this dictionary sat at namespace level, which
/// is not valid C#.
/// </summary>
public static class OrderStatusTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [OrderStatus.Refunded],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Refunded] = [],
    };

    public static bool IsAllowed(OrderStatus from, OrderStatus to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<OrderStatus> AllowedFrom(OrderStatus from)
        => Allowed.TryGetValue(from, out var targets) ? targets : [];
}
