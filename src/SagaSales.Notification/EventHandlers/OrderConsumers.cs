namespace SagaSales.Notification.EventHandlers;

/// <summary>
/// US-015: sends the order confirmation email when an order is placed.
/// </summary>
public sealed class OrderPlacedConsumer(
    Domain.INotificationDispatch dispatcher,
    ILogger<OrderPlacedConsumer> logger) : IntegrationEventHandler<OrderPlacedEvent>
{
    public override async Task Handle(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(@event.CustomerEmail))
        {
            logger.LogWarning(
                "Order {OrderId} has no customer email: confirmation email skipped.",
                @event.OrderId);

            return;
        }

        var lines = string.Join(
            "<br/>",
            @event.Items.Select(i => $"{i.ProductName} x{i.Quantity} = {i.Quantity * i.UnitPrice:C}"));

        var body = $"""
            <h2>Thank you for your order</h2>
            <p>Order <strong>{@event.OrderId}</strong> placed on {@event.OrderDate:u}.</p>
            <p>{lines}</p>
            <p><strong>Total: {@event.TotalAmount:C}</strong></p>
            """;

        await dispatcher.DispatchAsync(
            @event.TenantId,
            @event.CustomerId,
            NotificationType.Email,
            @event.CustomerEmail,
            $"Order confirmation {@event.OrderId}",
            body,
            cancellationToken);
    }
}

/// <summary>
/// US-016: alerts operations about high-value orders.
/// </summary>
public sealed class HighValueOrderAlertConsumer(
    Domain.INotificationDispatch dispatcher,
    IConfiguration configuration,
    ILogger<HighValueOrderAlertConsumer> logger) : IntegrationEventHandler<HighValueOrderAlertEvent>
{
    public override async Task Handle(HighValueOrderAlertEvent @event, CancellationToken cancellationToken)
    {
        var recipient = configuration["Notifications:OpsPhone"];

        if (string.IsNullOrWhiteSpace(recipient))
        {
            logger.LogWarning(
                "High-value order {OrderId} ({Amount}) detected but Notifications:OpsPhone is not configured.",
                @event.OrderId,
                @event.Amount);

            return;
        }

        await dispatcher.DispatchAsync(
            @event.TenantId,
            null,
            NotificationType.SMS,
            recipient,
            "High value order",
            $"Order {@event.OrderId} for {@event.Amount:C} on {@event.OrderDate:u}.",
            cancellationToken);
    }
}
