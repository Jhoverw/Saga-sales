namespace SagaSales.Payment.EventHandlers;

using MassTransit;
using SagaSales.Payment.Data;
using SagaSales.Shared.Events;

/// <summary>
/// Keeps a read model of the orders this service can be paid for, so Payment never reads the Sales
/// database (R4). Idempotent: the upsert can run twice for the same event.
/// </summary>
public sealed class OrderPlacedConsumer(
    IPaymentRepository repository,
    ILogger<OrderPlacedConsumer> logger) : IConsumer<OrderPlacedEvent>
{
    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var message = context.Message;

        await repository.UpsertOrderReferenceAsync(
            message.OrderId,
            message.CustomerId,
            message.TotalAmount,
            message.OrderDate,
            context.CancellationToken);

        logger.LogInformation("Order {OrderId} registered for payments", message.OrderId);
    }
}
