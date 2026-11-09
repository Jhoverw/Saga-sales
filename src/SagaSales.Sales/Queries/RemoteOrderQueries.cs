namespace SagaSales.Sales.Queries;

public record GetOrderFulfillmentQuery(Guid OrderId) : IQuery<OrderFulfillmentDto>;

public record GetRemoteOrdersQuery : IQuery<IReadOnlyList<RemoteOrderDto>>;

public record GetSalesSettingsQuery : IQuery<SalesSettingsDto>;

public sealed class RemoteOrderQueryHandlers(IOrderRepository repository) :
    IQueryHandler<GetOrderFulfillmentQuery, OrderFulfillmentDto>,
    IQueryHandler<GetRemoteOrdersQuery, IReadOnlyList<RemoteOrderDto>>,
    IQueryHandler<GetSalesSettingsQuery, SalesSettingsDto>
{
    public async Task<Result<OrderFulfillmentDto>> Handle(GetOrderFulfillmentQuery request, CancellationToken cancellationToken)
    {
        var fulfillment = await repository.GetFulfillmentAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        return Result<OrderFulfillmentDto>.Success(fulfillment);
    }

    public async Task<Result<IReadOnlyList<RemoteOrderDto>>> Handle(GetRemoteOrdersQuery request, CancellationToken cancellationToken)
        => Result<IReadOnlyList<RemoteOrderDto>>.Success(await repository.ListRemoteAsync(cancellationToken));

    public async Task<Result<SalesSettingsDto>> Handle(GetSalesSettingsQuery request, CancellationToken cancellationToken)
        => Result<SalesSettingsDto>.Success(await repository.GetSettingsAsync(cancellationToken));
}
