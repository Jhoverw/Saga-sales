namespace SagaSales.Shared.DTOs;

// ── branches ────────────────────────────────────────────────────────────────

public class BranchDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public bool IsDefault { get; set; }

    public short Status { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateBranchRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public bool IsDefault { get; set; }
}

public class UpdateBranchRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }
}

// ── settings (Parámetros) ───────────────────────────────────────────────────

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

// ── stock per branch and physical location ─────────────────────────────────

public class StockByBranchDto
{
    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public int Quantity { get; set; }

    /// <summary>Free text: "piso 1", "pasillo 3", "estanteria B".</summary>
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

// ── lots about to expire ────────────────────────────────────────────────────

public class ExpiringBatchDto
{
    public Guid BatchId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string LotCode { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }

    /// <summary>Negative when the lot is already expired.</summary>
    public int? DaysLeft { get; set; }

    public int Quantity { get; set; }
}
