namespace SagaSales.Sales.Queries;

public record GetOrderFulfillmentQuery(Guid OrderId) : IQuery<OrderFulfillmentDto>;

public record GetRemoteOrdersQuery : IQuery<IReadOnlyList<RemoteOrderDto>>;

public record GetSalesSettingsQuery : IQuery<SalesSettingsDto>;

public record GetReservedStockQuery : IQuery<IReadOnlyList<ReservedStockDto>>;

public record GetReservedStockDetailQuery(Guid ProductId) : IQuery<IReadOnlyList<ReservedOrderDto>>;

public sealed class RemoteOrderQueryHandlers(IOrderRepository repository) :
    IQueryHandler<GetOrderFulfillmentQuery, OrderFulfillmentDto>,
    IQueryHandler<GetRemoteOrdersQuery, IReadOnlyList<RemoteOrderDto>>,
    IQueryHandler<GetSalesSettingsQuery, SalesSettingsDto>,
    IQueryHandler<GetReservedStockQuery, IReadOnlyList<ReservedStockDto>>,
    IQueryHandler<GetReservedStockDetailQuery, IReadOnlyList<ReservedOrderDto>>
{
    public async Task<Result<IReadOnlyList<ReservedStockDto>>> Handle(GetReservedStockQuery request, CancellationToken cancellationToken)
        => Result<IReadOnlyList<ReservedStockDto>>.Success(await repository.ReservedStockAsync(cancellationToken));

    public async Task<Result<IReadOnlyList<ReservedOrderDto>>> Handle(GetReservedStockDetailQuery request, CancellationToken cancellationToken)
        => Result<IReadOnlyList<ReservedOrderDto>>.Success(await repository.ReservedStockDetailAsync(request.ProductId, cancellationToken));

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
