namespace SagaSales.Catalog.Queries;

/// <summary>
/// Paged product listing. Filtering happens before pagination and the total count uses the
/// same filter — the previous version applied Skip/Take first and counted unfiltered rows.
/// </summary>
public record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? CategoryId = null,
    string? Search = null,
    string? SortBy = null,
    bool Descending = false) : IQuery<PagedResult<ProductDto>>;

public sealed class GetProductsQueryHandler(
    CatalogDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<ProductDto>>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 20 : request.PageSize;

        var query = dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Status != ProductStatus.Discontinued);

        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(p => EF.Functions.Like(p.Name.ToLower(), $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = (request.SortBy?.ToLowerInvariant()) switch
        {
            "price" => request.Descending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "created" => request.Descending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            _ => request.Descending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<ProductDto>(
            items.Select(p => p.ToDto()).ToList(),
            pageNumber,
            pageSize,
            totalCount);

        return Result<PagedResult<ProductDto>>.Success(result);
    }
}
