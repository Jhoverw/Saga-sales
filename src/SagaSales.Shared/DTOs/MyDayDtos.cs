namespace SagaSales.Shared.DTOs;

/// <summary>Ventas de un vendedor o de la tienda en un rango (spec 006).</summary>
public class SellerSalesSummaryDto
{
    /// <summary>Un punto por día local, días sin ventas en 0.</summary>
    public List<DailySalesDto> Days { get; set; } = [];

    /// <summary>Productos vendidos en el día de hoy.</summary>
    public List<SoldProductDto> TodayProducts { get; set; } = [];

    public int TodayOrders { get; set; }

    public decimal TodayTotal { get; set; }

    /// <summary>Vendedores del período; vacío para un cajero.</summary>
    public List<SellerDto> Sellers { get; set; } = [];
}

public class DailySalesDto
{
    public DateOnly Day { get; set; }

    public int Orders { get; set; }

    public decimal Total { get; set; }
}

public class SoldProductDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal Total { get; set; }
}

public class SellerDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>Cobros del día por forma de pago y caja chica (spec 006).</summary>
public class PaymentDaySummaryDto
{
    public decimal Cash { get; set; }

    public decimal Card { get; set; }

    /// <summary>Transferencias y depósitos.</summary>
    public decimal Other { get; set; }

    public decimal Total => Cash + Card + Other;

    public int Payments { get; set; }

    /// <summary>Gastos de caja chica vigentes (no anulados) del rango.</summary>
    public decimal PettyCash { get; set; }

    /// <summary>Efectivo que debería haber en la caja: efectivo cobrado − caja chica.</summary>
    public decimal ExpectedCash => Cash - PettyCash;
}

public enum PettyCashCategory : short
{
    Maintenance = 1,
    Supplies = 2,
    Emergency = 3,
    Other = 9,
}

public class PettyCashExpenseDto
{
    public Guid Id { get; set; }

    public PettyCashCategory Category { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsVoided { get; set; }
}

public class CreatePettyCashExpenseRequest
{
    public PettyCashCategory Category { get; set; } = PettyCashCategory.Supplies;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

public class PettyCashSettingsDto
{
    /// <summary>Monto máximo por gasto.</summary>
    public decimal MaxAmount { get; set; }
}

/// <summary>Alertas de inventario visibles para el cajero: sin costos (spec 006).</summary>
public class InventoryAlertsDto
{
    public List<ExpiringBatchDto> Expiring { get; set; } = [];

    public List<LowStockDto> LowStock { get; set; } = [];
}

public class LowStockDto
{
    public Guid ProductId { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Stock { get; set; }

    public int? ReorderLevel { get; set; }
}
