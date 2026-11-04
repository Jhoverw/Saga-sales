namespace SagaSales.Shared.DTOs;

public class AddressDto
{
    public string Line1 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string StateProvince { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
}

public class CustomerDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public Enums.CustomerStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public IReadOnlyList<AddressDto> Addresses { get; set; } = [];
}

public class CreateCustomerRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public AddressDto? Address { get; set; }
}

public class UpdateCustomerRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public Enums.CustomerStatus Status { get; set; } = Enums.CustomerStatus.Active;
}
