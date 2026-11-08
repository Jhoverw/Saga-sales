// Inventory contracts — branches, store parameters and per-branch stock (E-13).
// Declarative contract: documents the API, it is not compiled.
// Runtime DTOs live in src/SagaSales.Shared/DTOs/InventoryDtos.cs.

namespace SagaSales.Contracts;

// ── branches ────────────────────────────────────────────────────────────────

public class BranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
    public short Status { get; set; }        // 1 active · 2 inactive
    public DateTime CreatedAt { get; set; }
}

public class CreateBranchRequest
{
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateBranchRequest
{
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
}

// ── store parameters ("Parámetros") ─────────────────────────────────────────

public class SettingsDto
{
    public int ExpiryWarningDays { get; set; }
    public Guid? DefaultBranchId { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateSettingsRequest
{
    public int ExpiryWarningDays { get; set; } = 30;
    public Guid? DefaultBranchId { get; set; }
}

// ── per-branch stock, location and lots ─────────────────────────────────────

public class StockByBranchDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public bool IsDefault { get; set; }
    public int Quantity { get; set; }
    public string? Floor { get; set; }
    public string? Aisle { get; set; }
    public string? Shelf { get; set; }
}

public class SetLocationRequest
{
    public Guid BranchId { get; set; }
    public string? Floor { get; set; }
    public string? Aisle { get; set; }
    public string? Shelf { get; set; }
}

public class ExpiringBatchDto
{
    public Guid BatchId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public string LotCode { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public int? DaysLeft { get; set; }       // negative when already expired
    public int Quantity { get; set; }
}

// HTTP Endpoints — all require a bearer token and a resolvable tenant (FR-004).
//
// POST   /api/branches                                    → 201 BranchDto · 409 duplicate name
// GET    /api/branches?includeInactive=false              → 200 BranchDto[]
// PUT    /api/branches/{id}                               → 200 BranchDto · 404 · 409 duplicate name
// DELETE /api/branches/{id}                               → 204 (the default branch cannot be deactivated → 409)
//
// GET    /api/settings                                    → 200 SettingsDto
// PUT    /api/settings                                    → 200 SettingsDto (1..365 days, branch must exist)
//
// GET    /api/inventory/products/{productId}/branches     → 200 StockByBranchDto[]
// PUT    /api/inventory/products/{productId}/location     → 204 (free-text floor/aisle/shelf)
// GET    /api/inventory/expiring?branchId=&maxRows=       → 200 ExpiringBatchDto[]
//
// Errors: 400 VALIDATION_ERROR · 401 · 403 · 404 NOT_FOUND · 409 CONFLICT
