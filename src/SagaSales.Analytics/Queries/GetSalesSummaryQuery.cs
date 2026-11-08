namespace SagaSales.Analytics.Queries;

/// <summary>
/// US-013/US-014: daily totals, revenue and top products for a date range.
/// </summary>
public record GetSalesSummaryQuery(DateOnly? From = null, DateOnly? To = null)
    : IQuery<SalesSummaryDto>;

public sealed class GetSalesSummaryQueryHandler(
    IAnalyticsRepository repository,
    ICurrentUser currentUser) : IQueryHandler<GetSalesSummaryQuery, SalesSummaryDto>
{
    public async Task<Result<SalesSummaryDto>> Handle(
        GetSalesSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? to.AddDays(-6);

        if (from > to)
        {
            throw new ConflictException("The 'from' date must be earlier than or equal to 'to'.");
        }

        var totals = await repository.GetSummaryAsync(tenantId, from, to, cancellationToken);
        var topProducts = await repository.GetTopProductsAsync(tenantId, from, to, cancellationToken);

        var summary = new SalesSummaryDto
        {
            From = from.ToDateTime(TimeOnly.MinValue),
            To = to.ToDateTime(TimeOnly.MaxValue),
            TotalRevenue = totals.TotalRevenue,
            OrderCount = totals.OrderCount,
            PaymentSuccessCount = totals.PaymentSuccessCount,
            TopProducts = topProducts,
        };

        return Result<SalesSummaryDto>.Success(summary);
    }
}
