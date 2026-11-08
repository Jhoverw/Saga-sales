namespace SagaSales.Shared.DTOs;

/// <summary>
/// One tax period as stored in <c>sales.tax_rates</c>. The column names are
/// <c>rate_percentage</c>, <c>start_date</c> and <c>end_date</c>, and the API aliases
/// them to these properties, so read and write agree.
/// </summary>
public class TaxRateDto
{
    public int Id { get; set; }

    /// <summary>Fraction, not percent: 0.15 means 15 %.</summary>
    public decimal RatePercentage { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Description { get; set; }
}
