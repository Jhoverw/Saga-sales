namespace SagaSales.Customer.Queries;

public record GetCustomerQuery(Guid Id) : IQuery<CustomerDto>;

public sealed class GetCustomerQueryHandler(
    CustomerDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetCustomerQuery, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(GetCustomerQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var customer = await dbContext.Customers
            .AsNoTracking()
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.TenantId == tenantId, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("Customer", request.Id);
        }

        return Result<CustomerDto>.Success(customer.ToDto());
    }
}
