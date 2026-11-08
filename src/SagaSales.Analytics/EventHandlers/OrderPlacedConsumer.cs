namespace SagaSales.Analytics.EventHandlers;

/// <summary>
/// Builds the daily sales aggregates from <c>OrderPlacedEvent</c>. The write happens entirely in
/// stored procedures (constitution §VIII); no ORM reads or tracks entities here.
/// </summary>
public sealed class OrderPlacedConsumer(
    IAnalyticsRepository repository,
    ILogger<OrderPlacedConsumer> logger)
    : IntegrationEventHandler<OrderPlacedEvent>
{
    public override async Task Handle(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(@event.OrderDate);

        await repository.UpsertOrderAsync(
            @event.TenantId,
            date,
            @event.TotalAmount,
            @event.Items,
            cancellationToken);

        logger.LogInformation("Analytics updated for tenant {TenantId} on {Date}", @event.TenantId, date);
    }
}
