namespace SagaSales.Analytics.Queries;

/// <summary>Day-by-day series for the dashboard chart (US-013).</summary>
public record GetDailySeriesQuery(DateOnly? From = null, DateOnly? To = null)
    : IQuery<IReadOnlyList<DailyPointDto>>;

public sealed class GetDailySeriesQueryHandler(
    IAnalyticsRepository repository,
    ICurrentUser currentUser) : IQueryHandler<GetDailySeriesQuery, IReadOnlyList<DailyPointDto>>
{
    public async Task<Result<IReadOnlyList<DailyPointDto>>> Handle(
        GetDailySeriesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? to.AddDays(-6);

        if (from > to)
        {
            throw new ConflictException("The 'from' date must be earlier than or equal to 'to'.");
        }

        var points = await repository.GetDailySeriesAsync(tenantId, from, to, cancellationToken);
        return Result<IReadOnlyList<DailyPointDto>>.Success(points);
    }
}
