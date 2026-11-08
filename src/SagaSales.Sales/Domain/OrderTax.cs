using System;

namespace SagaSales.Sales.Domain;

public class OrderTax
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid TaxRateId { get; set; }
    public decimal BaseImponible { get; set; }
    public decimal MontoImpuesto { get; set; }
}
