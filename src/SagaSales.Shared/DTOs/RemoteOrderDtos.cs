namespace SagaSales.Shared.DTOs;

/// <summary>Canal, apartado, verificación y envío de un pedido (specs 007 y 008).</summary>
public class OrderFulfillmentDto
{
    /// <summary>1 mostrador · 2 pedido.</summary>
    public int Channel { get; set; } = 1;

    public bool CashOnDelivery { get; set; }

    /// <summary>Hasta cuándo se aparta el stock sin pago verificado; null si no vence.</summary>
    public DateTime? HoldUntil { get; set; }

    /// <summary>Cuándo lo canceló el servicio de vencimiento.</summary>
    public DateTime? ExpiredAt { get; set; }

    public decimal VerifiedAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal Total { get; set; }

    public bool PaymentVerified => VerifiedAmount >= Total;

    public int? ShippingMethod { get; set; }

    public string? CourierCompany { get; set; }

    public string? TrackingNumber { get; set; }

    public DateTime? ShippedAt { get; set; }
}

/// <summary>Una fila del tablero de pedidos (spec 008).</summary>
public class RemoteOrderDto
{
    public Guid Id { get; set; }

    public string? OrderNumber { get; set; }

    public Guid CustomerId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal VerifiedAmount { get; set; }

    public bool CashOnDelivery { get; set; }

    public DateTime? HoldUntil { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public DateTime OrderDate { get; set; }

    public string? CreatedByName { get; set; }

    /// <summary>
    /// Columna del tablero: PorVerificar (falta el pago verificado), PorPreparar (verificado o contra entrega, aún
    /// sin preparar), EnPreparacion (listo para enviar), Enviado, Entregado, Anulado.
    /// </summary>
    public string Stage => Status switch
    {
        "Cancelled" or "Refunded" => "Anulado",
        "Delivered" => "Entregado",
        "Shipped" => "Enviado",
        "Processing" => "EnPreparacion",
        _ => CashOnDelivery || VerifiedAmount >= Total ? "PorPreparar" : "PorVerificar",
    };
}

public class SalesSettingsDto
{
    /// <summary>Horas que un pedido aparta el stock sin pago verificado (1 a 720).</summary>
    public int OrderHoldHours { get; set; } = 48;
}
