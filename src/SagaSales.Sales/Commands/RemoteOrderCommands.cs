namespace SagaSales.Sales.Commands;

/// <summary>Spec 008: Payment confirmed money it had recorded for this order.</summary>
public record OrderPaymentVerifiedCommand(Guid OrderId, decimal Amount) : ICommand<OrderDto>;

/// <summary>Spec 008: Payment rejected money that never arrived; it stops counting as paid.</summary>
public record OrderPaymentRejectedCommand(Guid OrderId, decimal Amount) : ICommand<OrderDto>;

public record SetSalesSettingsCommand(int OrderHoldHours) : ICommand<SalesSettingsDto>;

public sealed class RemoteOrderCommandHandlers(IOrderRepository repository, IEventBus eventBus) :
    ICommandHandler<OrderPaymentVerifiedCommand, OrderDto>,
    ICommandHandler<OrderPaymentRejectedCommand, OrderDto>,
    ICommandHandler<SetSalesSettingsCommand, SalesSettingsDto>
{
    public async Task<Result<OrderDto>> Handle(OrderPaymentVerifiedCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNegativeOrZero(request.Amount, nameof(request.Amount));

        var before = await repository.GetAsync(request.OrderId, cancellationToken)
                     ?? throw new NotFoundException("Order", request.OrderId);

        // Verifying the last missing amount moves a pedido to processing (inside the procedure).
        var order = await repository.PaymentVerifiedAsync(request.OrderId, request.Amount, cancellationToken);
        await PublishIfChangedAsync(before, order, cancellationToken);

        return Result<OrderDto>.Success(order.ToDto());
    }

    public async Task<Result<OrderDto>> Handle(OrderPaymentRejectedCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNegativeOrZero(request.Amount, nameof(request.Amount));

        var order = await repository.PaymentRejectedAsync(request.OrderId, request.Amount, cancellationToken);
        return Result<OrderDto>.Success(order.ToDto());
    }

    public async Task<Result<SalesSettingsDto>> Handle(SetSalesSettingsCommand request, CancellationToken cancellationToken)
    {
        if (request.OrderHoldHours is < 1 or > 720)
        {
            throw new SagaSales.Shared.Exceptions.ValidationException("Las horas de apartado van de 1 a 720.");
        }

        return Result<SalesSettingsDto>.Success(await repository.SetSettingsAsync(request.OrderHoldHours, cancellationToken));
    }

    private Task PublishIfChangedAsync(Order before, Order after, CancellationToken cancellationToken) =>
        before.Status == after.Status
            ? Task.CompletedTask
            : eventBus.PublishAsync(
                new OrderStatusChangedEvent(after.Id, before.TenantId, before.Status.ToString(), after.Status.ToString(), DateTime.UtcNow),
                cancellationToken);
}
