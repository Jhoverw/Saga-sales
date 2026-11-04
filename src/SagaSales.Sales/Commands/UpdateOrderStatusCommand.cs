namespace SagaSales.Sales.Commands;

using SagaSales.Sales.Domain;

public record UpdateOrderStatusCommand(Guid OrderId, OrderStatus NewStatus) : ICommand<OrderDto>;

public sealed class UpdateOrderStatusCommandHandler(
    SalesDbContext dbContext,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<UpdateOrderStatusCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(
        UpdateOrderStatusCommand request,
        CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.OrderId, nameof(request.OrderId));

        var tenantId = currentUser.RequireTenantId();

        var order = await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException("Order", request.OrderId);
        }

        var previous = order.Status;
        order.ChangeStatus(request.NewStatus);

        await dbContext.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new OrderStatusChangedEvent(
                order.Id,
                order.TenantId,
                previous.ToString(),
                order.Status.ToString(),
                DateTime.UtcNow),
            cancellationToken);

        return Result<OrderDto>.Success(order.ToDto());
    }
}
