namespace SagaSales.Sales.Commands;

/// <summary>
/// Records money received against an order. Called by the Payment service once a charge exists, so
/// the order reflects what was collected: <c>paid_amount</c> grows and a fully paid pending order
/// moves to processing.
/// </summary>
public record RegisterOrderPaymentCommand(Guid OrderId, decimal Amount) : ICommand<OrderDto>;

public sealed class RegisterOrderPaymentCommandHandler(
    IOrderRepository repository,
    IEventBus eventBus) : ICommandHandler<RegisterOrderPaymentCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(
        RegisterOrderPaymentCommand request,
        CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.OrderId, nameof(request.OrderId));
        Guard.AgainstNegativeOrZero(request.Amount, nameof(request.Amount));

        var before = await repository.GetAsync(request.OrderId, cancellationToken)
                     ?? throw new NotFoundException("Order", request.OrderId);

        // Validates that the order exists for this tenant and adds to paid_amount.
        var order = await repository.RegisterPaymentAsync(request.OrderId, request.Amount, cancellationToken);

        if (before.Status != order.Status)
        {
            await eventBus.PublishAsync(
                new OrderStatusChangedEvent(
                    order.Id,
                    before.TenantId,
                    before.Status.ToString(),
                    order.Status.ToString(),
                    DateTime.UtcNow),
                cancellationToken);
        }

        return Result<OrderDto>.Success(order.ToDto());
    }
}
