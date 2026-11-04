namespace SagaSales.Sales.Queries;

public record GetOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    OrderStatus? Status = null) : IQuery<PagedResult<OrderDto>>;

public sealed class GetOrdersQueryHandler(
    SalesDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetOrdersQuery, PagedResult<OrderDto>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<OrderDto>>> Handle(
        GetOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 20 : request.PageSize;

        var query = dbContext.Orders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId);

        if (request.Status is { } status)
        {
            query = query.Where(o => o.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<OrderDto>(
            orders.Select(o => o.ToDto()).ToList(),
            pageNumber,
            pageSize,
            totalCount);

        return Result<PagedResult<OrderDto>>.Success(result);
    }
}
