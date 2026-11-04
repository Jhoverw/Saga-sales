namespace SagaSales.Payment.EventHandlers;

/// <summary>
/// Keeps the Payment-owned order read model in sync, so payments never query the Sales DB.
/// </summary>
public sealed class OrderPlacedConsumer(PaymentDbContext dbContext, ILogger<OrderPlacedConsumer> logger)
    : IntegrationEventHandler<OrderPlacedEvent>
{
    public override async Task Handle(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        var exists = await dbContext.OrderReferences
            .AnyAsync(o => o.OrderId == @event.OrderId, cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.OrderReferences.Add(new Domain.OrderReference
        {
            OrderId = @event.OrderId,
            TenantId = @event.TenantId,
            CustomerId = @event.CustomerId,
            Subtotal = @event.TotalAmount,
            Currency = "USD",
            PlacedAt = @event.OrderDate,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Order reference stored for {OrderId}", @event.OrderId);
    }
}
