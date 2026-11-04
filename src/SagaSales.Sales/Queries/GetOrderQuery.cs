namespace SagaSales.Sales.Queries;

/// <summary>One order with its lines, which the order row alone cannot carry.</summary>
public record GetOrderQuery(Guid Id) : IQuery<OrderDto>;

public sealed class GetOrderQueryHandler(IOrderRepository repository)
    : IQueryHandler<GetOrderQuery, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await repository.GetAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException("Order", request.Id);

        var lines = await repository.GetLinesAsync(request.Id, cancellationToken);

        var dto = order.ToDto();
        dto.Items = lines.Select(l => l.ToDto()).ToList();

        return Result<OrderDto>.Success(dto);
    }
}
