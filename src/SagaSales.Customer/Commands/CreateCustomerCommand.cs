namespace SagaSales.Customer.Commands;

public record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    AddressDto? Address) : ICommand<CustomerDto>;

public sealed class CreateCustomerCommandHandler(
    CustomerDbContext dbContext,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.FirstName, nameof(request.FirstName));
        Guard.AgainstNullOrEmpty(request.LastName, nameof(request.LastName));
        Guard.AgainstNullOrEmpty(request.Email, nameof(request.Email));

        var tenantId = currentUser.RequireTenantId();
        var email = request.Email.Trim().ToLowerInvariant();

        var duplicate = await dbContext.Customers
            .AnyAsync(c => c.TenantId == tenantId && c.Email == email, cancellationToken);

        if (duplicate)
        {
            throw new ConflictException($"Customer with email '{email}' already exists for this tenant.");
        }

        var customer = new Domain.Customer
        {
            TenantId = tenantId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            Phone = request.Phone,
            Status = CustomerStatus.Active,
        };

        if (request.Address is { } address)
        {
            customer.Addresses.Add(new Domain.Address
            {
                CustomerId = customer.Id,
                Line1 = address.Line1,
                City = address.City,
                StateProvince = address.StateProvince,
                PostalCode = address.PostalCode,
                Country = address.Country,
            });
        }

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new CustomerCreatedEvent(
                customer.Id,
                customer.TenantId,
                customer.Email,
                customer.FirstName,
                customer.LastName),
            cancellationToken);

        return Result<CustomerDto>.Success(customer.ToDto());
    }
}
