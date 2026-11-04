namespace SagaSales.BlazorWeb.Services;

/// <summary>
/// Thin wrapper kept for backwards compatibility with existing pages.
/// </summary>
public sealed class AnalyticsService(ApiClient api)
{
    public Task<SalesSummaryDto?> GetSummaryAsync(DateOnly? from = null, DateOnly? to = null)
        => api.GetSalesSummaryAsync(from, to);
}
