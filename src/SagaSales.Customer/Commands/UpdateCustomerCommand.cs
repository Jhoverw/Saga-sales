namespace SagaSales.Customer.Commands;

public record UpdateCustomerCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? Phone,
    CustomerStatus Status) : ICommand<CustomerDto>;

public sealed class UpdateCustomerCommandHandler(
    CustomerDbContext dbContext,
    ICurrentUser currentUser) : ICommandHandler<UpdateCustomerCommand, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));
        Guard.AgainstNullOrEmpty(request.FirstName, nameof(request.FirstName));
        Guard.AgainstNullOrEmpty(request.LastName, nameof(request.LastName));

        var tenantId = currentUser.RequireTenantId();

        var customer = await dbContext.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.TenantId == tenantId, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("Customer", request.Id);
        }

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Phone = request.Phone;
        customer.Status = request.Status;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CustomerDto>.Success(customer.ToDto());
    }
}
