namespace SagaSales.Customer.Data;

using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;

/// <summary>
/// Customers and their addresses, through <c>db/customer/V002__functions.sql</c>.
/// No ORM: this repository is the only way into the customer database.
/// </summary>
public interface ICustomerRepository
{
    Task<CustomerDto> CreateAsync(
        string firstName,
        string lastName,
        string? email,
        string? phone,
        CustomerType customerType,
        AddressDto? address,
        IdentificationType? identificationType,
        string? identificationNumber,
        string? businessName,
        CancellationToken cancellationToken);

    Task<CustomerDto> UpdateAsync(
        Guid id,
        string firstName,
        string lastName,
        string? phone,
        CustomerType customerType,
        IdentificationType? identificationType,
        string? identificationNumber,
        string? businessName,
        CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AddressDto>> GetAddressesAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerListResult> ListAsync(
        int pageNumber,
        int pageSize,
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<AddressDto> AddAddressAsync(Guid customerId, AddressDto address, bool isDefault, CancellationToken cancellationToken);
}

public sealed record CustomerListResult(IReadOnlyList<CustomerDto> Items, int TotalCount);

public sealed class CustomerRepository(IStoredProcedureExecutor executor) : ICustomerRepository
{
    public async Task<CustomerDto> CreateAsync(
        string firstName,
        string lastName,
        string? email,
        string? phone,
        CustomerType customerType,
        AddressDto? address,
        IdentificationType? identificationType,
        string? identificationNumber,
        string? businessName,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("first_name", firstName)
            .With("last_name", lastName)
            .With("email", Nullify(email))
            .With("phone", Nullify(phone))
            .With("customer_type", (short)customerType)
            .With("line1", Nullify(address?.Line1))
            .With("city", Nullify(address?.City))
            .With("state_province", Nullify(address?.StateProvince))
            .With("postal_code", Nullify(address?.PostalCode))
            .With("country", Nullify(address?.Country))
            .With("identification_type", (short?)identificationType)
            .With("identification_number", Nullify(identificationNumber))
            .With("business_name", Nullify(businessName));

        // A duplicate email surfaces from the procedure as CONFLICT → 409.
        var row = await executor.QueryProcSingleAsync<CustomerRow>(
            "dbo.sp_customer_create", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task<CustomerDto> UpdateAsync(
        Guid id,
        string firstName,
        string lastName,
        string? phone,
        CustomerType customerType,
        IdentificationType? identificationType,
        string? identificationNumber,
        string? businessName,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("customer_id", id)
            .With("first_name", firstName)
            .With("last_name", lastName)
            .With("phone", Nullify(phone))
            .With("customer_type", (short)customerType)
            .With("identification_type", (short?)identificationType)
            .With("identification_number", Nullify(identificationNumber))
            .With("business_name", Nullify(businessName));

        var row = await executor.QueryProcSingleAsync<CustomerRow>(
            "dbo.sp_customer_update", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("customer_id", id);
        return executor.ExecuteProcAsync("dbo.sp_customer_soft_delete", arguments, cancellationToken: cancellationToken);
    }

    public async Task<CustomerDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("customer_id", id);

        var row = await executor.QueryProcSingleAsync<CustomerRow>(
            "dbo.sp_customer_get", arguments, cancellationToken: cancellationToken);

        return row?.ToDto();
    }

    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(Guid id, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("customer_id", id);

        var rows = await executor.QueryProcAsync<AddressRow>(
            "dbo.sp_address_list", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<CustomerListResult> ListAsync(
        int pageNumber,
        int pageSize,
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("page_number", pageNumber)
            .With("page_size", pageSize)
            .With("search", Nullify(search))
            .With("include_inactive", includeInactive);

        var rows = await executor.QueryProcAsync<CustomerRow>(
            "dbo.sp_customer_list", arguments, cancellationToken: cancellationToken);

        var items = rows.Select(r => r.ToDto()).ToList();
        var total = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new CustomerListResult(items, total);
    }

    public async Task<AddressDto> AddAddressAsync(
        Guid customerId,
        AddressDto address,
        bool isDefault,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("customer_id", customerId)
            .With("line1", address.Line1)
            .With("city", address.City)
            .With("state_province", address.StateProvince)
            .With("postal_code", address.PostalCode)
            .With("country", address.Country)
            .With("is_default", isDefault);

        var row = await executor.QueryProcSingleAsync<AddressRow>(
            "dbo.sp_address_add", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    private static string? Nullify(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class CustomerRow
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public short CustomerType { get; set; }

        public short Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public int TotalCount { get; set; }

        public short? IdentificationType { get; set; }

        public string? IdentificationNumber { get; set; }

        public string? BusinessName { get; set; }

        public CustomerDto ToDto() => new()
        {
            Id = Id,
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            Phone = Phone,
            CustomerType = (CustomerType)CustomerType,
            Status = (CustomerStatus)Status,
            CreatedAt = CreatedAt,
            IdentificationType = (SagaSales.Shared.Enums.IdentificationType?)IdentificationType,
            IdentificationNumber = IdentificationNumber,
            BusinessName = BusinessName,
        };
    }

    private sealed class AddressRow
    {
        public Guid Id { get; set; }

        public string Line1 { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string StateProvince { get; set; } = string.Empty;

        public string PostalCode { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        public AddressDto ToDto() => new()
        {
            Line1 = Line1,
            City = City,
            StateProvince = StateProvince,
            PostalCode = PostalCode,
            Country = Country,
        };
    }
}
