namespace SagaSales.Analytics.Queries;

/// <summary>Day-by-day series for the dashboard chart.</summary>
public record DailyPointDto(DateOnly Date, decimal Revenue, int Orders);

public record GetDailySeriesQuery(DateOnly? From = null, DateOnly? To = null)
    : IQuery<IReadOnlyList<DailyPointDto>>;

public sealed class GetDailySeriesQueryHandler(
    AnalyticsDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetDailySeriesQuery, IReadOnlyList<DailyPointDto>>
{
    public async Task<Result<IReadOnlyList<DailyPointDto>>> Handle(
        GetDailySeriesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? to.AddDays(-6);

        var points = await dbContext.SalesReports
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.ReportDate >= from && r.ReportDate <= to)
            .OrderBy(r => r.ReportDate)
            .Select(r => new DailyPointDto(r.ReportDate, r.TotalRevenue, r.OrderCount))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DailyPointDto>>.Success(points);
    }
}
