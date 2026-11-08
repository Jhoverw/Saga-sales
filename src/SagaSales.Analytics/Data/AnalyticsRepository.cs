namespace SagaSales.Analytics.Data;

using Dapper;
using SagaSales.BuildingBlocks.Data;

/// <summary>
/// Analytics aggregates through the procedures in <c>db/analytics/V002</c>.
/// The consumers run outside an HTTP request, so every method takes the tenant explicitly
/// and passes <c>p_tenant_id</c> itself — the database enforces isolation, not the caller.
/// </summary>
public interface IAnalyticsRepository
{
    Task UpsertOrderAsync(
        Guid tenantId,
        DateOnly reportDate,
        decimal totalAmount,
        IReadOnlyList<OrderItemSnapshot> items,
        CancellationToken cancellationToken);

    Task UpsertPaymentAsync(Guid tenantId, DateOnly reportDate, CancellationToken cancellationToken);

    Task AddActivityAsync(
        Guid tenantId,
        Guid customerId,
        string eventType,
        DateTime occurredAt,
        CancellationToken cancellationToken);

    Task<SalesTotals> GetSummaryAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyPointDto>> GetDailySeriesAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

/// <summary>Aggregate totals for a date range (US-013).</summary>
public sealed record SalesTotals(decimal TotalRevenue, int OrderCount, int PaymentSuccessCount);

public sealed class AnalyticsRepository(IStoredProcedureExecutor executor) : IAnalyticsRepository
{
    public async Task UpsertOrderAsync(
        Guid tenantId,
        DateOnly reportDate,
        decimal totalAmount,
        IReadOnlyList<OrderItemSnapshot> items,
        CancellationToken cancellationToken)
    {
        var date = ToDate(reportDate);

        await executor.ExecuteProcAsync(
            "dbo.sp_report_order_upsert",
            SqlArgs.New()
                .With("p_tenant_id", tenantId)
                .With("report_date", date)
                .With("total_amount", totalAmount),
            cancellationToken: cancellationToken);

        foreach (var item in items)
        {
            await executor.ExecuteProcAsync(
                "dbo.sp_product_sales_upsert",
                SqlArgs.New()
                    .With("p_tenant_id", tenantId)
                    .With("report_date", date)
                    .With("product_id", item.ProductId)
                    .With("product_name", item.ProductName)
                    .With("quantity", item.Quantity)
                    .With("revenue", item.Quantity * item.UnitPrice),
                cancellationToken: cancellationToken);
        }
    }

    public Task UpsertPaymentAsync(Guid tenantId, DateOnly reportDate, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_report_payment_upsert",
            SqlArgs.New()
                .With("p_tenant_id", tenantId)
                .With("report_date", ToDate(reportDate)),
            cancellationToken: cancellationToken);

    public Task AddActivityAsync(
        Guid tenantId,
        Guid customerId,
        string eventType,
        DateTime occurredAt,
        CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_activity_add",
            SqlArgs.New()
                .With("p_tenant_id", tenantId)
                .With("customer_id", customerId)
                .With("event_type", eventType)
                .With("occurred_at", occurredAt),
            cancellationToken: cancellationToken);

    public async Task<SalesTotals> GetSummaryAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var row = await executor.QueryProcSingleAsync<SummaryRow>(
            "dbo.sp_sales_summary_get",
            SqlArgs.New()
                .With("p_tenant_id", tenantId)
                .With("from_date", ToDate(from))
                .With("to_date", ToDate(to)),
            cancellationToken: cancellationToken);

        return row is null
            ? new SalesTotals(0, 0, 0)
            : new SalesTotals(row.TotalRevenue, row.OrderCount, row.PaymentSuccessCount);
    }

    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryProcAsync<TopProductRow>(
            "dbo.sp_top_products_get",
            SqlArgs.New()
                .With("p_tenant_id", tenantId)
                .With("from_date", ToDate(from))
                .With("to_date", ToDate(to)),
            cancellationToken: cancellationToken);

        return rows.Select(r => new TopProductDto
        {
            ProductId = r.ProductId,
            ProductName = r.ProductName,
            UnitsSold = r.UnitsSold,
            Revenue = r.Revenue,
        }).ToList();
    }

    public async Task<IReadOnlyList<DailyPointDto>> GetDailySeriesAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryProcAsync<DailyRow>(
            "dbo.sp_daily_series_get",
            SqlArgs.New()
                .With("p_tenant_id", tenantId)
                .With("from_date", ToDate(from))
                .With("to_date", ToDate(to)),
            cancellationToken: cancellationToken);

        return rows.Select(r => new DailyPointDto
        {
            Date = DateOnly.FromDateTime(r.Date),
            Revenue = r.Revenue,
            Orders = r.Orders,
        }).ToList();
    }

    private static DateTime ToDate(DateOnly value) => value.ToDateTime(TimeOnly.MinValue);

    private sealed class SummaryRow
    {
        public decimal TotalRevenue { get; set; }

        public int OrderCount { get; set; }

        public int PaymentSuccessCount { get; set; }
    }

    private sealed class TopProductRow
    {
        public Guid ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public int UnitsSold { get; set; }

        public decimal Revenue { get; set; }
    }

    private sealed class DailyRow
    {
        public DateTime Date { get; set; }

        public decimal Revenue { get; set; }

        public int Orders { get; set; }
    }
}
