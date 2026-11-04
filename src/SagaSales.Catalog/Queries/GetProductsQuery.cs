namespace SagaSales.Catalog.Queries;

/// <summary>
/// Paged product listing. Filtering happens before pagination inside <c>sp_product_list</c>, and
/// the total count comes from the same filtered set (FR-009).
/// </summary>
public record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? CategoryId = null,
    string? Search = null,
    string? SortBy = null,
    bool Descending = false) : IQuery<PagedResult<ProductDto>>;

public sealed class GetProductsQueryHandler(IProductRepository repository)
    : IQueryHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    public async Task<Result<PagedResult<ProductDto>>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var result = await repository.ListAsync(
            pageNumber,
            pageSize,
            request.CategoryId,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            request.SortBy,
            request.Descending,
            cancellationToken);

        var page = new PagedResult<ProductDto>(
            result.Items.Select(p => p.ToDto()).ToList(),
            pageNumber,
            pageSize,
            result.TotalCount);

        return Result<PagedResult<ProductDto>>.Success(page);
    }
}
