namespace SagaSales.Sales.Queries;

/// <summary>Datos del envío de un pedido (spec 007).</summary>
public record GetOrderShipmentQuery(Guid OrderId) : IQuery<OrderShipmentDto>;

public sealed class GetOrderShipmentQueryHandler(IOrderRepository repository)
    : IQueryHandler<GetOrderShipmentQuery, OrderShipmentDto>
{
    public async Task<Result<OrderShipmentDto>> Handle(GetOrderShipmentQuery request, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetShipmentAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        return Result<OrderShipmentDto>.Success(shipment);
    }
}
