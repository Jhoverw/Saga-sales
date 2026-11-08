namespace SagaSales.Shared.DTOs;

public record CreateTaxRateRequest(
    decimal RatePercentage,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Description);

