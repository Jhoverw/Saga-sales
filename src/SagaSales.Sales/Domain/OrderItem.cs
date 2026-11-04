namespace SagaSales.Sales.Domain;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>Price and name are captured at order time (US-004).</summary>
    public string ProductSnapshotName { get; set; } = string.Empty;

    public decimal ProductSnapshotPrice { get; set; }

    public int ProductSnapshotStockAtPurchase { get; set; }

    public Order? Order { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
