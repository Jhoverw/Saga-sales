namespace SagaSales.Sales.Commands;

using SagaSales.Sales.Infrastructure;

/// <summary>
/// Cancels an order and gives its stock back.
/// <para>
/// The stock is released only for the lines of this order, and only after the state change is
/// accepted: releasing first and then failing the transition would invent inventory. If a release
/// fails, the cancellation still stands (the order is cancelled) and the failure is compensated by
/// the kardex, which keeps the trace of what was and was not returned.
/// </para>
/// </summary>
public record CancelOrderCommand(Guid OrderId) : ICommand<OrderDto>;

public sealed class CancelOrderCommandHandler(
    IOrderRepository repository,
    ICatalogClient catalogClient,
    IEventBus eventBus,
    ILogger<CancelOrderCommandHandler> logger) : ICommandHandler<CancelOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.OrderId, nameof(request.OrderId));

        var order = await repository.GetAsync(request.OrderId, cancellationToken)
                    ?? throw new NotFoundException("Order", request.OrderId);

        var lines = await repository.GetLinesAsync(request.OrderId, cancellationToken);
        var previous = order.Status;

        // INVALID_TRANSITION comes from the procedure and maps to 409.
        var cancelled = await repository.ChangeStatusAsync(request.OrderId, OrderStatus.Cancelled, cancellationToken);

        foreach (var line in lines)
        {
            try
            {
                await catalogClient.ReleaseStockAsync(line.ProductId, line.Quantity, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Order {OrderId} cancelled but product {ProductId} could not be returned ({Quantity} units)",
                    order.Id,
                    line.ProductId,
                    line.Quantity);
            }
        }

        await eventBus.PublishAsync(
            new OrderStatusChangedEvent(
                order.Id,
                order.TenantId,
                previous.ToString(),
                cancelled.Status.ToString(),
                DateTime.UtcNow),
            cancellationToken);

        return Result<OrderDto>.Success(cancelled.ToDtoWith(lines));
    }
}
