namespace SagaSales.Payment.Mapping;

using SagaSales.Payment.Domain;

public static class PaymentMapping
{
    public static PaymentDto ToDto(this Payment payment) => new()
    {
        Id = payment.Id,
        OrderId = payment.OrderId,
        CustomerId = payment.CustomerId,
        Method = payment.Method,
        Status = payment.Status,
        Amount = payment.Amount,
        RefundedAmount = payment.RefundedAmount,
        Currency = payment.Currency,
        TransactionReference = payment.TransactionReference,
        CreatedAt = payment.CreatedAt,
        PaidAt = payment.PaidAt,
        CapturedAt = payment.CapturedAt,
        RefundedAt = payment.RefundedAt,
    };
}
