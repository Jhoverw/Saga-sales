namespace SagaSales.Catalog.Queries;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

public record GetBranchesQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<BranchDto>>;

public sealed class GetBranchesQueryHandler(IBranchRepository repository)
    : IQueryHandler<GetBranchesQuery, IReadOnlyList<BranchDto>>
{
    public async Task<Result<IReadOnlyList<BranchDto>>> Handle(
        GetBranchesQuery request,
        CancellationToken cancellationToken)
    {
        var branches = await repository.ListAsync(request.IncludeInactive, cancellationToken);
        return Result<IReadOnlyList<BranchDto>>.Success(branches);
    }
}

public record GetSettingsQuery : IQuery<SettingsDto>;

public sealed class GetSettingsQueryHandler(IBranchRepository repository)
    : IQueryHandler<GetSettingsQuery, SettingsDto>
{
    public async Task<Result<SettingsDto>> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await repository.GetSettingsAsync(cancellationToken);
        return Result<SettingsDto>.Success(settings);
    }
}

public record GetStockByBranchQuery(Guid ProductId) : IQuery<IReadOnlyList<StockByBranchDto>>;

public sealed class GetStockByBranchQueryHandler(IBranchRepository repository)
    : IQueryHandler<GetStockByBranchQuery, IReadOnlyList<StockByBranchDto>>
{
    public async Task<Result<IReadOnlyList<StockByBranchDto>>> Handle(
        GetStockByBranchQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await repository.GetStockByBranchAsync(request.ProductId, cancellationToken);
        return Result<IReadOnlyList<StockByBranchDto>>.Success(stock);
    }
}

public record GetExpiringBatchesQuery(Guid? BranchId = null, int MaxRows = 100)
    : IQuery<IReadOnlyList<ExpiringBatchDto>>;

public sealed class GetExpiringBatchesQueryHandler(IBranchRepository repository)
    : IQueryHandler<GetExpiringBatchesQuery, IReadOnlyList<ExpiringBatchDto>>
{
    public async Task<Result<IReadOnlyList<ExpiringBatchDto>>> Handle(
        GetExpiringBatchesQuery request,
        CancellationToken cancellationToken)
    {
        var batches = await repository.GetExpiringAsync(request.BranchId, request.MaxRows, cancellationToken);
        return Result<IReadOnlyList<ExpiringBatchDto>>.Success(batches);
    }
}
