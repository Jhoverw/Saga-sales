namespace SagaSales.Catalog.Data;

using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.DTOs;

/// <summary>
/// Branches, store settings, stock per branch and lots about to expire, through
/// <c>db/catalog/V009</c> and <c>V010</c>.
/// </summary>
public interface IBranchRepository
{
    Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken);

    Task<BranchDto> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<BranchDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<SettingsDto> GetSettingsAsync(CancellationToken cancellationToken);

    Task<SettingsDto> UpdateSettingsAsync(UpdateSettingsRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockByBranchDto>> GetStockByBranchAsync(Guid productId, CancellationToken cancellationToken);

    Task SetLocationAsync(Guid productId, SetLocationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpiringBatchDto>> GetExpiringAsync(Guid? branchId, int maxRows, CancellationToken cancellationToken);
}

public sealed class BranchRepository(IStoredProcedureExecutor executor) : IBranchRepository
{
    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("name", request.Name.Trim())
            .With("address", Trim(request.Address))
            .With("phone", Trim(request.Phone))
            .With("is_default", request.IsDefault);

        var row = await executor.QueryProcSingleAsync<BranchRow>(
            "dbo.sp_branch_create", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task<BranchDto> UpdateAsync(
        Guid id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("branch_id", id)
            .With("name", request.Name.Trim())
            .With("address", Trim(request.Address))
            .With("phone", Trim(request.Phone));

        var row = await executor.QueryProcSingleAsync<BranchRow>(
            "dbo.sp_branch_update", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("branch_id", id);
        return executor.ExecuteProcAsync("dbo.sp_branch_deactivate", arguments, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<BranchDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("include_inactive", includeInactive);

        var rows = await executor.QueryProcAsync<BranchRow>(
            "dbo.sp_branch_list", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<SettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var row = await executor.QueryProcSingleAsync<SettingsRow>(
            "dbo.sp_settings_get", cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task<SettingsDto> UpdateSettingsAsync(
        UpdateSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("expiry_warning_days", request.ExpiryWarningDays)
            .With("default_branch_id", request.DefaultBranchId);

        var row = await executor.QueryProcSingleAsync<SettingsRow>(
            "dbo.sp_settings_update", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task<IReadOnlyList<StockByBranchDto>> GetStockByBranchAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("product_id", productId);

        var rows = await executor.QueryProcAsync<StockByBranchRow>(
            "dbo.sp_stock_by_branch", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task SetLocationAsync(
        Guid productId,
        SetLocationRequest request,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("product_id", productId)
            .With("branch_id", request.BranchId)
            .With("floor", Trim(request.Floor))
            .With("aisle", Trim(request.Aisle))
            .With("shelf", Trim(request.Shelf));

        await executor.QueryProcAsync<object>(
            "dbo.sp_location_set", arguments, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ExpiringBatchDto>> GetExpiringAsync(
        Guid? branchId,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("branch_id", branchId)
            .With("max_rows", maxRows);

        var rows = await executor.QueryProcAsync<ExpiringBatchRow>(
            "dbo.sp_expiring_batches", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class BranchRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string? Phone { get; set; }

        public bool IsDefault { get; set; }

        public short Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public BranchDto ToDto() => new()
        {
            Id = Id,
            Name = Name,
            Address = Address,
            Phone = Phone,
            IsDefault = IsDefault,
            Status = Status,
            CreatedAt = CreatedAt,
        };
    }

    private sealed class SettingsRow
    {
        public int ExpiryWarningDays { get; set; }

        public Guid? DefaultBranchId { get; set; }

        public DateTime UpdatedAt { get; set; }

        public SettingsDto ToDto() => new()
        {
            ExpiryWarningDays = ExpiryWarningDays,
            DefaultBranchId = DefaultBranchId,
            UpdatedAt = UpdatedAt,
        };
    }

    private sealed class StockByBranchRow
    {
        public Guid BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        public int Quantity { get; set; }

        public string? Floor { get; set; }

        public string? Aisle { get; set; }

        public string? Shelf { get; set; }

        public StockByBranchDto ToDto() => new()
        {
            BranchId = BranchId,
            BranchName = BranchName,
            IsDefault = IsDefault,
            Quantity = Quantity,
            Floor = Floor,
            Aisle = Aisle,
            Shelf = Shelf,
        };
    }

    private sealed class ExpiringBatchRow
    {
        public Guid BatchId { get; set; }

        public Guid ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public Guid BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string LotCode { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }

        public int? DaysLeft { get; set; }

        public int Quantity { get; set; }

        public ExpiringBatchDto ToDto() => new()
        {
            BatchId = BatchId,
            ProductId = ProductId,
            ProductName = ProductName,
            BranchId = BranchId,
            BranchName = BranchName,
            LotCode = LotCode,
            ExpiresAt = ExpiresAt,
            DaysLeft = DaysLeft,
            Quantity = Quantity,
        };
    }
}
