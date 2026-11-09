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

    /// <summary>Whether the product carries VAT (spec 003, FR-003).</summary>
    public bool IsTaxable { get; set; } = true;

    /// <summary>True when UnitPrice is the PVP (VAT included); false for lines priced before spec 003.</summary>
    public bool PriceIncludesTax { get; set; }

    // Amounts computed and stored by sp_order_create (zero until the order is saved and read back).
    public decimal UnitPriceWithTax { get; set; }

    public decimal LineNet { get; set; }

    public decimal LineTax { get; set; }

    public decimal LineTotal { get; set; }

    public Order? Order { get; set; }

}
