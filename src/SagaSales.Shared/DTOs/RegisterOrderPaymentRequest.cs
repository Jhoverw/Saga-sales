namespace SagaSales.Shared.DTOs;

/// <summary>Money received for an order, as reported by the Payment service.</summary>
public class RegisterOrderPaymentRequest
{
    public decimal Amount { get; set; }

    /// <summary>
    /// Falso para una transferencia, depósito o tarjeta de un pedido que alguien debe confirmar (spec 008).
    /// Por omisión verdadero: el efectivo y las ventas de mostrador llegan verificados.
    /// </summary>
    public bool Verified { get; set; } = true;
}

/// <summary>Monto de un pago que se verificó o se rechazó (spec 008).</summary>
public class OrderPaymentAdjustmentRequest
{
    public decimal Amount { get; set; }
}
