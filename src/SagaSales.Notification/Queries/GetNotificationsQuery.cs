namespace SagaSales.Notification.Queries;

public record GetNotificationsQuery(int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResult<NotificationDto>>;

public sealed class GetNotificationsQueryHandler(
    NotificationDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetNotificationsQuery, PagedResult<NotificationDto>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<NotificationDto>>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 20 : request.PageSize;

        var query = dbContext.NotificationLogs
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<NotificationDto>(
            items.Select(n => n.ToDto()).ToList(),
            pageNumber,
            pageSize,
            totalCount);

        return Result<PagedResult<NotificationDto>>.Success(result);
    }
}
