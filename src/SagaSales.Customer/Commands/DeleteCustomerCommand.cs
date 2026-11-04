namespace SagaSales.Customer.Commands;

public record DeleteCustomerCommand(Guid Id) : ICommand;

public sealed class DeleteCustomerCommandHandler(
    CustomerDbContext dbContext,
    ICurrentUser currentUser) : ICommandHandler<DeleteCustomerCommand>
{
    public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));

        var tenantId = currentUser.RequireTenantId();

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.TenantId == tenantId, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("Customer", request.Id);
        }

        // Soft delete keeps order history intact.
        customer.Status = CustomerStatus.Deleted;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
