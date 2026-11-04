namespace SagaSales.Analytics.EventHandlers;

/// <summary>
/// Builds the daily sales aggregates from <c>OrderPlacedEvent</c>.
/// </summary>
public sealed class OrderPlacedConsumer(AnalyticsDbContext dbContext, ILogger<OrderPlacedConsumer> logger)
    : IntegrationEventHandler<OrderPlacedEvent>
{
    public override async Task Handle(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(@event.OrderDate);

        var report = await dbContext.SalesReports
            .FirstOrDefaultAsync(r => r.TenantId == @event.TenantId && r.ReportDate == date, cancellationToken);

        if (report is null)
        {
            report = new Domain.SalesReportDaily
            {
                TenantId = @event.TenantId,
                ReportDate = date,
            };

            dbContext.SalesReports.Add(report);
        }

        report.TotalRevenue += @event.TotalAmount;
        report.OrderCount += 1;
        report.GeneratedAt = DateTime.UtcNow;

        foreach (var item in @event.Items)
        {
            var productSales = await dbContext.ProductSales
                .FirstOrDefaultAsync(
                    p => p.TenantId == @event.TenantId && p.ReportDate == date && p.ProductId == item.ProductId,
                    cancellationToken);

            if (productSales is null)
            {
                productSales = new Domain.ProductSalesDaily
                {
                    TenantId = @event.TenantId,
                    ReportDate = date,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                };

                dbContext.ProductSales.Add(productSales);
            }

            productSales.UnitsSold += item.Quantity;
            productSales.Revenue += item.Quantity * item.UnitPrice;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Analytics updated for tenant {TenantId} on {Date}", @event.TenantId, date);
    }
}
