namespace SagaSales.Catalog.Data;

using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.DTOs;

/// <summary>Inventory control report and reorder settings, through db/catalog/V025 (spec 004).</summary>
public interface IInventoryReportRepository
{
    Task<IReadOnlyList<InventoryReportRowDto>> GetReportAsync(Guid? branchId, CancellationToken cancellationToken);

    Task SetSettingsAsync(Guid productId, InventorySettingsRequest request, CancellationToken cancellationToken);
}

public sealed class InventoryReportRepository(IStoredProcedureExecutor executor) : IInventoryReportRepository
{
    public Task<IReadOnlyList<InventoryReportRowDto>> GetReportAsync(Guid? branchId, CancellationToken cancellationToken)
        => executor.QueryProcAsync<InventoryReportRowDto>(
            "dbo.sp_inventory_report",
            SqlArgs.New().With("branch_id", branchId),
            cancellationToken: cancellationToken);

    public Task SetSettingsAsync(Guid productId, InventorySettingsRequest request, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("product_id", productId)
            .With("code", request.Code)
            .With("manufacturer", request.Manufacturer)
            .With("reorder_level", request.ReorderLevel)
            .With("reorder_quantity", request.ReorderQuantity)
            .With("lead_time_days", request.LeadTimeDays)
            .With("notes", request.Notes);

        // A repeated code surfaces from the procedure as CONFLICT → 409.
        return executor.ExecuteProcAsync(
            "dbo.sp_product_set_inventory_settings", arguments, cancellationToken: cancellationToken);
    }
}
