namespace SagaSales.Sales.Queries;

/// <summary>
/// Paged order list. The lines are not loaded here on purpose: the list view does not show them,
/// and fetching them per row would be a query per order (N+1).
/// </summary>
public record GetOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    OrderStatus? Status = null) : IQuery<PagedResult<OrderDto>>;

public sealed class GetOrdersQueryHandler(IOrderRepository repository)
    : IQueryHandler<GetOrdersQuery, PagedResult<OrderDto>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<OrderDto>>> Handle(
        GetOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 20 : request.PageSize;

        var result = await repository.ListAsync(pageNumber, pageSize, request.Status, cancellationToken);

        var page = new PagedResult<OrderDto>(
            result.Items.Select(o => o.ToDto()).ToList(),
            pageNumber,
            pageSize,
            result.TotalCount);

        return Result<PagedResult<OrderDto>>.Success(page);
    }
}
