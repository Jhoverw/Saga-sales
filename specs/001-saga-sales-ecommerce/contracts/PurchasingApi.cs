// Purchasing contracts — suppliers, purchase documents and their lines (E-12).
// Declarative contract: documents the API, it is not compiled.
// Runtime DTOs live in src/SagaSales.Shared/DTOs/PurchasingDtos.cs.

namespace SagaSales.Contracts;

// ── suppliers ───────────────────────────────────────────────────────────────

public class SupplierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? TaxId { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public short Status { get; set; }        // 1 active · 2 inactive
    public DateTime CreatedAt { get; set; }
}

public class CreateSupplierRequest
{
    public string Name { get; set; } = "";
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

/// <summary>A purchase document: header plus one or more lines (FR-032/FR-050).</summary>
public class PurchaseDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid? BranchId { get; set; }
    public short Status { get; set; }        // 1 draft · 2 received · 3 cancelled
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
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public bool CostIncludesTax { get; set; }
    public decimal? TaxRate { get; set; }
    /// <summary>Normalised, always tax-included: what feeds the weighted average cost.</summary>
    public decimal EffectiveUnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public string? LotCode { get; set; }     // required when the product tracks batches
    public DateTime? ExpiresAt { get; set; }
}

public class CreatePurchaseRequest
{
    public Guid SupplierId { get; set; }
    public Guid? BranchId { get; set; }      // null: the store's default branch
    /// <summary>True by default: registering and receiving is one action. False keeps a draft.</summary>
    public bool ReceiveNow { get; set; } = true;
    public string? SupplierInvoiceNumber { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public string? Notes { get; set; }

    /// <summary>One or many lines; the same product may repeat with different lots.</summary>
    public IList<PurchaseLineRequest> Lines { get; set; } = [];
}

public class PurchaseLineRequest
{
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public bool CostIncludesTax { get; set; } = true;
    public decimal? TaxRate { get; set; }
    public string? LotCode { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

// HTTP Endpoints — all require a bearer token and a resolvable tenant (FR-004).
//
// POST   /api/suppliers                                   → 201 SupplierDto · 409 duplicate tax id
// GET    /api/suppliers?includeInactive=&search=           → 200 SupplierDto[]
// PUT    /api/suppliers/{id}                              → 200 SupplierDto · 404 · 409
// DELETE /api/suppliers/{id}                              → 204 (logical deactivate)
//
// POST   /api/purchases                                   → 201 PurchaseDto (header + all lines)
//        body: CreatePurchaseRequest; receiveNow=true receives in the same transaction.
// GET    /api/purchases?pageNumber=&pageSize=&status=     → 200 PagedResult<PurchaseDto>
// GET    /api/purchases/{id}                              → 200 PurchaseDto with its lines · 404
// POST   /api/purchases/{id}/receive?branchId=            → 200 PurchaseDto · 409 already received
//
// Rules: every line needs quantity > 0 and unit_cost > 0; a lot-tracked product needs lotCode on
// its line; receiving recalculates the weighted average cost and writes the kardex (FR-034/FR-035).
//
// Errors: 400 VALIDATION_ERROR · 401 · 403 · 404 NOT_FOUND · 409 CONFLICT | INVALID_TRANSITION
