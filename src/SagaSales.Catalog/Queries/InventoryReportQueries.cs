namespace SagaSales.Catalog.Queries;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

/// <summary>Inventory control report, optionally for one branch (spec 004).</summary>
public record GetInventoryReportQuery(Guid? BranchId) : IQuery<IReadOnlyList<InventoryReportRowDto>>;

public sealed class GetInventoryReportQueryHandler(IInventoryReportRepository repository)
    : IQueryHandler<GetInventoryReportQuery, IReadOnlyList<InventoryReportRowDto>>
{
    public async Task<Result<IReadOnlyList<InventoryReportRowDto>>> Handle(
        GetInventoryReportQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await repository.GetReportAsync(request.BranchId, cancellationToken);
        return Result<IReadOnlyList<InventoryReportRowDto>>.Success(rows);
    }
}
