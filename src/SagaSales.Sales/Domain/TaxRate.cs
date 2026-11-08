using System;

namespace SagaSales.Sales.Domain;

public class TaxRate
{
    public Guid Id { get; set; }
    public decimal Rate { get; set; } // e.g., 0.12m for 12%
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Description { get; set; } = default!;
}
