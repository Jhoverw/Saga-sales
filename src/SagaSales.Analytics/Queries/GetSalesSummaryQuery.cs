namespace SagaSales.Analytics.Queries;

/// <summary>
/// US-013/US-014: daily totals, revenue and top products for a date range.
/// </summary>
public record GetSalesSummaryQuery(DateOnly? From = null, DateOnly? To = null)
    : IQuery<SalesSummaryDto>;

public sealed class GetSalesSummaryQueryHandler(
    AnalyticsDbContext dbContext,
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

        var reports = await dbContext.SalesReports
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.ReportDate >= from && r.ReportDate <= to)
            .ToListAsync(cancellationToken);

        var topProducts = await dbContext.ProductSales
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.ReportDate >= from && p.ReportDate <= to)
            .GroupBy(p => new { p.ProductId, p.ProductName })
            .Select(g => new TopProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                UnitsSold = g.Sum(x => x.UnitsSold),
                Revenue = g.Sum(x => x.Revenue),
            })
            .OrderByDescending(p => p.Revenue)
            .Take(5)
            .ToListAsync(cancellationToken);

        var summary = new SalesSummaryDto
        {
            From = from.ToDateTime(TimeOnly.MinValue),
            To = to.ToDateTime(TimeOnly.MaxValue),
            TotalRevenue = reports.Sum(r => r.TotalRevenue),
            OrderCount = reports.Sum(r => r.OrderCount),
            PaymentSuccessCount = reports.Sum(r => r.PaymentSuccessCount),
            TopProducts = topProducts,
        };

        return Result<SalesSummaryDto>.Success(summary);
    }
}
