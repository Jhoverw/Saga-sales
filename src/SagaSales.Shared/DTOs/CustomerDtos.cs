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

    /// <summary>Drives which price list applies when this customer orders (D5).</summary>
    public Enums.CustomerType CustomerType { get; set; }

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

    /// <summary>Defaults to retail when omitted (FR-007a).</summary>
    public Enums.CustomerType CustomerType { get; set; } = Enums.CustomerType.Retail;

    public AddressDto? Address { get; set; }
}

public class UpdateCustomerRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public Enums.CustomerType CustomerType { get; set; } = Enums.CustomerType.Retail;

    public Enums.CustomerStatus Status { get; set; } = Enums.CustomerStatus.Active;
}
