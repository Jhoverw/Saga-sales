namespace SagaSales.Customer.Queries;

using SagaSales.Customer.Data;

/// <summary>One customer with its addresses, which the row alone cannot carry.</summary>
public record GetCustomerQuery(Guid Id) : IQuery<CustomerDto>;

public sealed class GetCustomerQueryHandler(ICustomerRepository repository)
    : IQueryHandler<GetCustomerQuery, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(GetCustomerQuery request, CancellationToken cancellationToken)
    {
        var customer = await repository.GetAsync(request.Id, cancellationToken)
                       ?? throw new NotFoundException("Customer", request.Id);

        customer.Addresses = await repository.GetAddressesAsync(request.Id, cancellationToken);
        return Result<CustomerDto>.Success(customer);
    }
}
