namespace SagaSales.Shared.DTOs;

/// <summary>Money received for an order, as reported by the Payment service.</summary>
public class RegisterOrderPaymentRequest
{
    public decimal Amount { get; set; }
}
