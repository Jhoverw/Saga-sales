namespace SagaSales.Customer.Commands;

using SagaSales.Customer.Data;

/// <summary>
/// Edits a buyer. The customer type lives here because it decides which price list applies when
/// this customer orders (D5). Identification and business name can be corrected (spec 002).
/// </summary>
public record UpdateCustomerCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? Phone = null,
    CustomerStatus Status = CustomerStatus.Active,
    CustomerType CustomerType = CustomerType.Retail,
    IdentificationType? IdentificationType = null,
    string? IdentificationNumber = null,
    string? BusinessName = null) : ICommand<CustomerDto>;

public sealed class UpdateCustomerCommandHandler(ICustomerRepository repository)
    : ICommandHandler<UpdateCustomerCommand, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));

        var customer = await repository.UpdateAsync(
            request.Id,
            request.FirstName?.Trim() ?? string.Empty,
            request.LastName?.Trim() ?? string.Empty,
            request.Phone,
            request.CustomerType,
            request.IdentificationType,
            request.IdentificationNumber?.Trim(),
            request.BusinessName?.Trim(),
            cancellationToken);

        return Result<CustomerDto>.Success(customer);
    }
}
