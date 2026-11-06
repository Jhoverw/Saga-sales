namespace SagaSales.Customer.Queries;

using SagaSales.Customer.Data;

/// <summary>
/// Paged customer search. The search covers name, email and phone because at the counter the
/// operator has whichever of the three the buyer gives them.
/// </summary>
public record GetCustomersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    bool IncludeInactive = false) : IQuery<PagedResult<CustomerDto>>;

public sealed class GetCustomersQueryHandler(ICustomerRepository repository)
    : IQueryHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    public async Task<Result<PagedResult<CustomerDto>>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var result = await repository.ListAsync(
            pageNumber,
            pageSize,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            request.IncludeInactive,
            cancellationToken);

        var page = new PagedResult<CustomerDto>(result.Items, pageNumber, pageSize, result.TotalCount);
        return Result<PagedResult<CustomerDto>>.Success(page);
    }
}
