namespace SagaSales.Shared.DTOs;

public class CreatePreSaleRequest
{
    public Guid ProductId { get; set; }
    public Guid CustomerId { get; set; }
    public int Qty { get; set; }
    public DateTime ExpectedDate { get; set; }
}

public class PreSaleDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid CustomerId { get; set; }
    public int Qty { get; set; }
    public DateTime ExpectedDate { get; set; }
    public string Status { get; set; } = default!;
}
