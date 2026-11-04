namespace SagaSales.Sales.Queries;

public record GetOrderQuery(Guid Id) : IQuery<OrderDto>;

public sealed class GetOrderQueryHandler(
    SalesDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetOrderQuery, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException("Order", request.Id);
        }

        return Result<OrderDto>.Success(order.ToDto());
    }
}
