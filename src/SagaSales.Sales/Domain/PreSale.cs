using System;

namespace SagaSales.Sales.Domain;

public class PreSale
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid CustomerId { get; set; }
    public int Qty { get; set; }
    public DateTime ExpectedDate { get; set; }
    public PreSaleStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum PreSaleStatus
{
    Pendiente = 0,
    Listo = 1,
    Cancelado = 2,
}
