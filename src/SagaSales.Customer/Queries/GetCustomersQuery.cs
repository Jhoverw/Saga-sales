namespace SagaSales.Customer.Queries;

/// <summary>
/// Lists customers, optionally searching by email or phone (US-012).
/// </summary>
public record GetCustomersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null) : IQuery<PagedResult<CustomerDto>>;

public sealed class GetCustomersQueryHandler(
    CustomerDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<CustomerDto>>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 20 : request.PageSize;

        var query = dbContext.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Status != CustomerStatus.Deleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                EF.Functions.Like(c.Email.ToLower(), $"%{term}%") ||
                (c.Phone != null && EF.Functions.Like(c.Phone.ToLower(), $"%{term}%")) ||
                EF.Functions.Like(c.FirstName.ToLower(), $"%{term}%") ||
                EF.Functions.Like(c.LastName.ToLower(), $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .Include(c => c.Addresses)
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<CustomerDto>(
            customers.Select(c => c.ToDto()).ToList(),
            pageNumber,
            pageSize,
            totalCount);

        return Result<PagedResult<CustomerDto>>.Success(result);
    }
}
