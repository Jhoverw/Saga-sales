namespace SagaSales.Shared.DTOs;

// ── suppliers ───────────────────────────────────────────────────────────────

public class SupplierDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? TaxId { get; set; }

    public string? ContactName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public short Status { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateSupplierRequest
{
    public string Name { get; set; } = string.Empty;

    public string? TaxId { get; set; }

    public string? ContactName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Notes { get; set; }
}

public class UpdateSupplierRequest : CreateSupplierRequest
{
}

// ── purchases ───────────────────────────────────────────────────────────────

public class PurchaseDto
{
    public Guid Id { get; set; }

    public Guid SupplierId { get; set; }

    public string? SupplierName { get; set; }

    /// <summary>The branch the goods land in. Null on drafts created before E-13.</summary>
    public Guid? BranchId { get; set; }

    /// <summary>1 draft · 2 received · 3 cancelled.</summary>
    public short Status { get; set; }

    public string? SupplierInvoiceNumber { get; set; }

    public DateTime PurchasedAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Total { get; set; }

    public string? Notes { get; set; }

    public IReadOnlyList<PurchaseLineDto> Lines { get; set; } = [];
}

public class PurchaseLineDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    /// <summary>The cost exactly as the supplier quoted it.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>True when <see cref="UnitCost"/> already includes tax.</summary>
    public bool CostIncludesTax { get; set; }

    public decimal? TaxRate { get; set; }

    /// <summary>Normalised, always tax-included: what feeds the weighted average cost.</summary>
    public decimal EffectiveUnitCost { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>Lot the goods came in, when the product tracks batches.</summary>
    public string? LotCode { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// A purchase as the operator types it. The same shape serves the quick entry (one line) and the
/// full document (many lines): the difference is the screen, not the model.
/// </summary>
public class CreatePurchaseRequest
{
    public Guid SupplierId { get; set; }

    /// <summary>Where the goods land. Null: the store's default branch.</summary>
    public Guid? BranchId { get; set; }

    /// <summary>
    /// Receive in the same call. True by default: registering a purchase and entering the inventory
    /// look like one action. Set it to false for a big order that arrives later.
    /// </summary>
    public bool ReceiveNow { get; set; } = true;

    public string? SupplierInvoiceNumber { get; set; }

    public DateTime? PurchasedAt { get; set; }

    public string? Notes { get; set; }

    public IList<PurchaseLineRequest> Lines { get; set; } = [];
}

public class PurchaseLineRequest
{
    public Guid ProductId { get; set; }

    public string? ProductName { get; set; }

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    /// <summary>True when the quoted cost already includes tax; false when tax is added on top.</summary>
    public bool CostIncludesTax { get; set; } = true;

    public decimal? TaxRate { get; set; }

    /// <summary>Required when the product tracks lots.</summary>
    public string? LotCode { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

// ── kardex ──────────────────────────────────────────────────────────────────

public class StockMovementDto
{
    public Guid Id { get; set; }

    /// <summary>1 purchase in · 2 sale out · 3 adjustment · 4 return in.</summary>
    public short Kind { get; set; }

    /// <summary>Signed: positive brings stock in, negative takes it out.</summary>
    public int Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public int StockAfter { get; set; }

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public DateTime OccurredAt { get; set; }
}
