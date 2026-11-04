namespace SagaSales.Analytics.EventHandlers;

public sealed class PaymentAuthorizedConsumer(AnalyticsDbContext dbContext)
    : IntegrationEventHandler<PaymentAuthorizedEvent>
{
    public override async Task Handle(PaymentAuthorizedEvent @event, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(@event.AuthorizedAt);

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

        report.PaymentSuccessCount += 1;
        report.GeneratedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CustomerCreatedConsumer(AnalyticsDbContext dbContext)
    : IntegrationEventHandler<CustomerCreatedEvent>
{
    public override async Task Handle(CustomerCreatedEvent @event, CancellationToken cancellationToken)
    {
        dbContext.CustomerActivities.Add(new Domain.CustomerActivity
        {
            TenantId = @event.TenantId,
            CustomerId = @event.CustomerId,
            EventType = "customer.created",
            OccurredAt = @event.OccurredOn,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
