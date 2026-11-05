namespace SagaSales.Catalog.Queries;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

// ── suppliers ───────────────────────────────────────────────────────────────

public record GetSuppliersQuery(bool IncludeInactive = false, string? Search = null)
    : IQuery<IReadOnlyList<SupplierDto>>;

public sealed class GetSuppliersQueryHandler(ISupplierRepository repository)
    : IQueryHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    public async Task<Result<IReadOnlyList<SupplierDto>>> Handle(
        GetSuppliersQuery request,
        CancellationToken cancellationToken)
    {
        var suppliers = await repository.ListAsync(
            request.IncludeInactive,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            cancellationToken);

        return Result<IReadOnlyList<SupplierDto>>.Success(suppliers);
    }
}

// ── purchases ───────────────────────────────────────────────────────────────

public record GetPurchasesQuery(int PageNumber = 1, int PageSize = 20, short? Status = null)
    : IQuery<PagedResult<PurchaseDto>>;

public sealed class GetPurchasesQueryHandler(IPurchaseRepository repository)
    : IQueryHandler<GetPurchasesQuery, PagedResult<PurchaseDto>>
{
    public async Task<Result<PagedResult<PurchaseDto>>> Handle(
        GetPurchasesQuery request,
        CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var result = await repository.ListAsync(pageNumber, pageSize, request.Status, cancellationToken);

        var page = new PagedResult<PurchaseDto>(result.Items, pageNumber, pageSize, result.TotalCount);
        return Result<PagedResult<PurchaseDto>>.Success(page);
    }
}

public record GetPurchaseQuery(Guid Id) : IQuery<PurchaseDto>;

public sealed class GetPurchaseQueryHandler(IPurchaseRepository repository)
    : IQueryHandler<GetPurchaseQuery, PurchaseDto>
{
    public async Task<Result<PurchaseDto>> Handle(GetPurchaseQuery request, CancellationToken cancellationToken)
    {
        var purchase = await repository.GetAsync(request.Id, cancellationToken)
                       ?? throw new NotFoundException("Purchase", request.Id);

        purchase.Lines = await repository.GetLinesAsync(request.Id, cancellationToken);
        return Result<PurchaseDto>.Success(purchase);
    }
}

// ── kardex ──────────────────────────────────────────────────────────────────

public record GetStockMovementsQuery(Guid ProductId, int MaxRows = 50)
    : IQuery<IReadOnlyList<StockMovementDto>>;

public sealed class GetStockMovementsQueryHandler(IPurchaseRepository repository)
    : IQueryHandler<GetStockMovementsQuery, IReadOnlyList<StockMovementDto>>
{
    public async Task<Result<IReadOnlyList<StockMovementDto>>> Handle(
        GetStockMovementsQuery request,
        CancellationToken cancellationToken)
    {
        var movements = await repository.ListMovementsAsync(request.ProductId, request.MaxRows, cancellationToken);
        return Result<IReadOnlyList<StockMovementDto>>.Success(movements);
    }
}
