namespace SagaSales.Shared.DTOs;

/// <summary>One row of the inventory control report (spec 004, FR-002).</summary>
public class InventoryReportRowDto
{
    public Guid ProductId { get; set; }

    /// <summary>Item number set by the store; optional.</summary>
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    public string? Description { get; set; }

    /// <summary>Weighted average cost; null when the product was never received in a purchase.</summary>
    public decimal? UnitCost { get; set; }

    public int Stock { get; set; }

    /// <summary>Cost × stock, or null when the cost is unknown (FR-003).</summary>
    public decimal? InventoryValue { get; set; }

    public int? ReorderLevel { get; set; }

    public int? LeadTimeDays { get; set; }

    public DateTime? LastSaleAt { get; set; }

    public int? ReorderQuantity { get; set; }

    public string? Notes { get; set; }

    /// <summary>Supplier of the last received purchase that included the product (FR-006).</summary>
    public string? LastSupplier { get; set; }

    /// <summary>Stock at or below the reorder level (FR-004).</summary>
    public bool NeedsReorder { get; set; }
}

/// <summary>Reorder data of a product. Saving it never changes price or stock (FR-008).</summary>
public class InventorySettingsRequest
{
    public string? Code { get; set; }

    public string? Manufacturer { get; set; }

    public int? ReorderLevel { get; set; }

    public int? ReorderQuantity { get; set; }

    public int? LeadTimeDays { get; set; }

    public string? Notes { get; set; }
}
