namespace SagaSales.Sales.Commands;

using SagaSales.Sales.Domain;

/// <summary>
/// Moves an order along the state machine. The allowed transitions are enforced inside
/// <c>sp_order_change_status</c>, so an invalid step returns 409 instead of corrupting the row.
/// </summary>
public record UpdateOrderStatusCommand(Guid OrderId, OrderStatus NewStatus) : ICommand<OrderDto>;

public sealed class UpdateOrderStatusCommandHandler(
    IOrderRepository repository,
    IEventBus eventBus) : ICommandHandler<UpdateOrderStatusCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(
        UpdateOrderStatusCommand request,
        CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.OrderId, nameof(request.OrderId));

        // Read first so the event can carry the previous status.
        var current = await repository.GetAsync(request.OrderId, cancellationToken)
                      ?? throw new NotFoundException("Order", request.OrderId);

        var previous = current.Status;

        // NOT_FOUND and INVALID_TRANSITION come from the procedure.
        var order = await repository.ChangeStatusAsync(request.OrderId, request.NewStatus, cancellationToken);

        await eventBus.PublishAsync(
            new OrderStatusChangedEvent(
                order.Id,
                current.TenantId,
                previous.ToString(),
                order.Status.ToString(),
                DateTime.UtcNow),
            cancellationToken);

        return Result<OrderDto>.Success(order.ToDto());
    }
}
