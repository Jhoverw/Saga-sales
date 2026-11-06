namespace SagaSales.Customer.Commands;

using SagaSales.Customer.Data;

/// <summary>
/// Registers a buyer. The first address comes with the customer: an order always needs a shipping
/// address (D7), so asking for it later would leave orders that cannot be shipped.
/// </summary>
public record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string Email,
    string? Phone = null,
    CustomerType CustomerType = CustomerType.Retail,
    AddressDto? Address = null) : ICommand<CustomerDto>;

public sealed class CreateCustomerCommandHandler(ICustomerRepository repository)
    : ICommandHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.FirstName, nameof(request.FirstName));
        Guard.AgainstNullOrEmpty(request.LastName, nameof(request.LastName));
        Guard.AgainstNullOrEmpty(request.Email, nameof(request.Email));

        // A duplicate email surfaces from the procedure as CONFLICT → 409.
        var customer = await repository.CreateAsync(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            request.Phone,
            request.CustomerType,
            request.Address,
            cancellationToken);

        return Result<CustomerDto>.Success(customer);
    }
}
