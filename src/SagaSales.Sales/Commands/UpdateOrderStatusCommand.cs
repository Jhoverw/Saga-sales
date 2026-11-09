namespace SagaSales.Sales.Commands;

using SagaSales.Sales.Domain;

/// <summary>
/// Moves an order along the state machine. The allowed transitions are enforced inside
/// <c>sp_order_change_status</c>, so an invalid step returns 409 instead of corrupting the row.
/// </summary>
public record UpdateOrderStatusCommand(Guid OrderId, OrderStatus NewStatus, ShipmentRequest? Shipment = null)
    : ICommand<OrderDto>;

public sealed class UpdateOrderStatusCommandHandler(
    IOrderRepository repository,
    IEventBus eventBus,
    IPaymentClient payments) : ICommandHandler<UpdateOrderStatusCommand, OrderDto>
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
        var order = request.NewStatus == OrderStatus.Shipped && previous == OrderStatus.Processing
            ? await ShipAsync(request, cancellationToken)
            : await repository.ChangeStatusAsync(request.OrderId, request.NewStatus, cancellationToken);

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

    /// <summary>
    /// Spec 007, FR-003: un pedido sale solo con forma de pago registrada y con comprobante en cada pago que
    /// no es en efectivo; por courier, además, con empresa y guía (eso lo valida el procedure).
    /// </summary>
    private async Task<Order> ShipAsync(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var shipment = request.Shipment
            ?? throw new SagaSales.Shared.Exceptions.ValidationException("Indica cómo sale el pedido: courier (con empresa y guía) o entrega propia.");

        // Spec 008: contra entrega sale sin pago todavía; se cobra al entregar.
        var fulfillment = await repository.GetFulfillmentAsync(request.OrderId, cancellationToken);
        if (fulfillment is { CashOnDelivery: true })
        {
            return await repository.ShipAsync(request.OrderId, shipment, cancellationToken);
        }

        var proof = await payments.GetProofStatusAsync(request.OrderId, cancellationToken);

        if (proof.Payments == 0)
        {
            throw new ConflictException("Registra la forma de pago del pedido antes de enviarlo.");
        }

        if (proof.WithoutProof > 0)
        {
            throw new ConflictException(proof.WithoutProof == 1
                ? "Falta el comprobante de un pago que no es en efectivo. Sube la foto o el PDF antes de enviar."
                : $"Faltan los comprobantes de {proof.WithoutProof} pagos que no son en efectivo. Súbelos antes de enviar.");
        }

        return await repository.ShipAsync(request.OrderId, shipment, cancellationToken);
    }
}
