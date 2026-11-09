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

/// <summary>
/// Alertas para el panel "Mi día" (spec 006): lotes por vencer y productos en su nivel de reorden o
/// agotados. Sale del mismo reporte de inventario pero sin costos, porque lo ve el cajero.
/// </summary>
public record GetInventoryAlertsQuery(Guid? BranchId, int MaxRows = 50) : IQuery<InventoryAlertsDto>;

public sealed class GetInventoryAlertsQueryHandler(IInventoryReportRepository reports, IMediator mediator)
    : IQueryHandler<GetInventoryAlertsQuery, InventoryAlertsDto>
{
    public async Task<Result<InventoryAlertsDto>> Handle(
        GetInventoryAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var expiring = await mediator.Send(new GetExpiringBatchesQuery(request.BranchId, request.MaxRows), cancellationToken);
        var rows = await reports.GetReportAsync(request.BranchId, cancellationToken);

        return Result<InventoryAlertsDto>.Success(new InventoryAlertsDto
        {
            Expiring = (expiring.Value ?? []).ToList(),
            LowStock = ToLowStock(rows, request.MaxRows),
        });
    }

    /// <summary>Agotados primero, después los que más bajo están respecto a su nivel de reorden.</summary>
    public static List<LowStockDto> ToLowStock(IEnumerable<InventoryReportRowDto> rows, int maxRows) =>
        rows.Where(r => r.Stock <= 0 || (r.ReorderLevel is { } level && r.Stock <= level))
            .OrderBy(r => r.Stock <= 0 ? 0 : 1)
            .ThenBy(r => r.Stock - (r.ReorderLevel ?? 0))
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(maxRows)
            .Select(r => new LowStockDto
            {
                ProductId = r.ProductId,
                Code = r.Code,
                Name = r.Name,
                Stock = r.Stock,
                ReorderLevel = r.ReorderLevel,
            })
            .ToList();
}
