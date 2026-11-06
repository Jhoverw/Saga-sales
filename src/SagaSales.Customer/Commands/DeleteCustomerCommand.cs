namespace SagaSales.Customer.Commands;

using SagaSales.Customer.Data;

/// <summary>
/// Deactivates a buyer (soft delete): the orders that point at it must keep resolving, so the row
/// is never removed. It also stops appearing when placing a new order.
/// </summary>
public record DeleteCustomerCommand(Guid Id) : ICommand;

public sealed class DeleteCustomerCommandHandler(ICustomerRepository repository)
    : ICommandHandler<DeleteCustomerCommand>
{
    public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));

        await repository.DeactivateAsync(request.Id, cancellationToken);
        return Result.Success();
    }
}
