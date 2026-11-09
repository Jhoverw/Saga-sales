namespace SagaSales.Sales.Data;

using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.Time;

/// <summary>Ventas por vendedor para el panel "Mi día" (spec 006), vía <c>db/sales/V027</c>.</summary>
public interface ISellerSummaryRepository
{
    Task<IReadOnlyList<DailySalesDto>> GetDailyAsync(LocalDayRange range, Guid? sellerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SoldProductDto>> GetProductsAsync(LocalDayRange range, Guid? sellerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SellerDto>> GetSellersAsync(DateTime fromUtc, CancellationToken cancellationToken);
}

public sealed class SellerSummaryRepository(IStoredProcedureExecutor executor) : ISellerSummaryRepository
{
    public async Task<IReadOnlyList<DailySalesDto>> GetDailyAsync(
        LocalDayRange range,
        Guid? sellerId,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("from_utc", range.FromUtc)
            .With("to_utc", range.ToUtc)
            .With("offset_minutes", range.OffsetMinutes)
            .With("seller_id", sellerId);

        var rows = await executor.QueryProcAsync<DailyRow>(
            "dbo.sp_order_seller_daily", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => new DailySalesDto
        {
            Day = DateOnly.FromDateTime(r.Day),
            Orders = r.Orders,
            Total = r.Total,
        }).ToList();
    }

    public async Task<IReadOnlyList<SoldProductDto>> GetProductsAsync(
        LocalDayRange range,
        Guid? sellerId,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("from_utc", range.FromUtc)
            .With("to_utc", range.ToUtc)
            .With("seller_id", sellerId);

        var rows = await executor.QueryProcAsync<SoldProductDto>(
            "dbo.sp_order_seller_products", arguments, cancellationToken: cancellationToken);

        return rows.ToList();
    }

    public async Task<IReadOnlyList<SellerDto>> GetSellersAsync(DateTime fromUtc, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("from_utc", fromUtc);

        var rows = await executor.QueryProcAsync<SellerDto>(
            "dbo.sp_order_sellers", arguments, cancellationToken: cancellationToken);

        return rows.ToList();
    }

    // Dapper no convierte date → DateOnly en todas las versiones; se lee como DateTime.
    private sealed class DailyRow
    {
        public DateTime Day { get; set; }

        public int Orders { get; set; }

        public decimal Total { get; set; }
    }
}
