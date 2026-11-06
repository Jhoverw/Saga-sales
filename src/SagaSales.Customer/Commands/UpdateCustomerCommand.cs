namespace SagaSales.Customer.Commands;

using SagaSales.Customer.Data;

/// <summary>
/// Edits a buyer. The customer type lives here because it decides which price list applies when
/// this customer orders (D5).
/// </summary>
public record UpdateCustomerCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? Phone = null,
    CustomerStatus Status = CustomerStatus.Active,
    CustomerType CustomerType = CustomerType.Retail) : ICommand<CustomerDto>;

public sealed class UpdateCustomerCommandHandler(ICustomerRepository repository)
    : ICommandHandler<UpdateCustomerCommand, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));
        Guard.AgainstNullOrEmpty(request.FirstName, nameof(request.FirstName));
        Guard.AgainstNullOrEmpty(request.LastName, nameof(request.LastName));

        var customer = await repository.UpdateAsync(
            request.Id,
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Phone,
            request.CustomerType,
            cancellationToken);

        return Result<CustomerDto>.Success(customer);
    }
}
