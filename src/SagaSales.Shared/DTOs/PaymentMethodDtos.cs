namespace SagaSales.Shared.DTOs;

public class CashPaymentDto
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountReceived { get; set; }
}

public class CardPaymentDto
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? CardLast4 { get; set; }
}

public class CreditPaymentDto
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
}
