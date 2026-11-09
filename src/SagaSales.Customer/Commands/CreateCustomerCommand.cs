namespace SagaSales.Customer.Commands;

using SagaSales.Customer.Data;

/// <summary>
/// Registers a buyer. The first address comes with the customer: an order always needs a shipping
/// address (D7), so asking for it later would leave orders that cannot be shipped. Since spec 002
/// the email is optional and a company is named by its business name.
/// </summary>
public record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone = null,
    CustomerType CustomerType = CustomerType.Retail,
    AddressDto? Address = null,
    IdentificationType? IdentificationType = null,
    string? IdentificationNumber = null,
    string? BusinessName = null) : ICommand<CustomerDto>;

public sealed class CreateCustomerCommandHandler(ICustomerRepository repository)
    : ICommandHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Names, business name and identification are checked by CreateCustomerCommandValidator;
        // duplicates (email or identification) surface from the procedure as CONFLICT → 409.
        var customer = await repository.CreateAsync(
            request.FirstName?.Trim() ?? string.Empty,
            request.LastName?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            request.Phone,
            request.CustomerType,
            request.Address,
            request.IdentificationType,
            request.IdentificationNumber?.Trim(),
            request.BusinessName?.Trim(),
            cancellationToken);

        return Result<CustomerDto>.Success(customer);
    }
}
