namespace SagaSales.Shared.DTOs;

/// <summary>Una compra ingresada como la factura del proveedor (spec 009).</summary>
public class PurchaseInvoiceRequest
{
    public Guid SupplierId { get; set; }

    public Guid? BranchId { get; set; }

    public bool ReceiveNow { get; set; } = true;

    /// <summary>1 factura · 2 nota de venta · 3 liquidación de compra.</summary>
    public int DocumentType { get; set; } = 1;

    /// <summary>Establecimiento-punto-secuencial, p. ej. 001-002-000000123.</summary>
    public string DocumentNumber { get; set; } = string.Empty;

    public string? AuthorizationNumber { get; set; }

    public DateOnly IssuedAt { get; set; }

    /// <summary>Fecha de ingreso a bodega; vacía = ahora.</summary>
    public DateTime? ReceivedAt { get; set; }

    public DateOnly? DocumentExpiresAt { get; set; }

    /// <summary>Código de forma de pago del SRI (1 sin sistema financiero, 16 débito, 19 crédito, 20 otros con sistema financiero...).</summary>
    public int? PaymentMethod { get; set; }

    public bool IsCredit { get; set; }

    public DateOnly? CreditDueAt { get; set; }

    /// <summary>Tasa de IVA como fracción (0.15).</summary>
    public decimal VatRate { get; set; }

    public decimal DiscountZero { get; set; }

    public decimal DiscountTaxed { get; set; }

    public decimal Ice { get; set; }

    public decimal OtherCharges { get; set; }

    public string? Notes { get; set; }

    public List<PurchaseInvoiceLineRequest> Lines { get; set; } = [];
}

public class PurchaseInvoiceLineRequest
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    /// <summary>Cantidad como viene en la factura (en su empaque).</summary>
    public decimal Quantity { get; set; }

    /// <summary>"unidad" o "caja".</summary>
    public string Package { get; set; } = "unidad";

    public int UnitsPerPackage { get; set; } = 1;

    /// <summary>Precio de un empaque sin IVA.</summary>
    public decimal UnitPrice { get; set; }

    public bool Taxable { get; set; } = true;

    public string? LotCode { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Producto como lo necesita el detalle de la compra.</summary>
public class PurchaseProductDto
{
    public Guid Id { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsTaxable { get; set; }

    public int? UnitsPerPackage { get; set; }

    public decimal? Cost { get; set; }

    public bool TracksBatches { get; set; }

    public decimal BasePrice { get; set; }
}

/// <summary>Producto nuevo creado desde una línea de la compra.</summary>
public class CreatePurchaseProductRequest
{
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Precio de venta al público, con IVA si lo grava.</summary>
    public decimal SalePrice { get; set; }

    public bool IsTaxable { get; set; } = true;

    public int? UnitsPerPackage { get; set; }

    public bool TracksBatches { get; set; }

    /// <summary>Código de barras escaneado en la línea (spec 010, HU-10.3); se guarda en el producto.</summary>
    public string? Barcode { get; set; }

    /// <summary>Unidades que representa ese código: 1 la unidad, más de 1 la caja.</summary>
    public int BarcodeUnits { get; set; } = 1;
}

/// <summary>Lo que se debe a un proveedor por una compra a crédito.</summary>
public class PayableDto
{
    public Guid Id { get; set; }

    public Guid PurchaseId { get; set; }

    public Guid SupplierId { get; set; }

    public string SupplierName { get; set; } = string.Empty;

    public int? DocumentType { get; set; }

    public string? DocumentNumber { get; set; }

    public DateTime? IssuedAt { get; set; }

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public DateTime DueAt { get; set; }

    /// <summary>1 abierta · 2 pagada · 3 anulada.</summary>
    public int Status { get; set; }
}

public class PayablePaymentRequest
{
    public decimal Amount { get; set; }

    public DateOnly PaidAt { get; set; }

    /// <summary>1 efectivo · 2 transferencia · 3 cheque · 9 otro.</summary>
    public int Method { get; set; } = 2;

    public string? Reference { get; set; }
}

public class PayablePaymentDto
{
    public Guid Id { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaidAt { get; set; }

    public int Method { get; set; }

    public string? Reference { get; set; }
}
