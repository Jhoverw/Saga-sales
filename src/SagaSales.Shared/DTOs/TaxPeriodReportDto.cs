namespace SagaSales.Shared.DTOs;

public class TaxPeriodReportDto
{
    public decimal Rate { get; set; } // e.g., 0.12
    public DateTime From { get; set; }
    public DateTime? To { get; set; }
    public decimal TotalTaxCollected { get; set; }
}
