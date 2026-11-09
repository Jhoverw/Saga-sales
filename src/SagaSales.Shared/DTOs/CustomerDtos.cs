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

    /// <summary>Optional since spec 002 (quick sign-up at the till); unique per store when present.</summary>
    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>Drives which price list applies when this customer orders (D5).</summary>
    public Enums.CustomerType CustomerType { get; set; }

    public Enums.CustomerStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>SRI identification type (spec 002, FR-003).</summary>
    public Enums.IdentificationType? IdentificationType { get; set; }

    public string? IdentificationNumber { get; set; }

    /// <summary>Companies (RUC) are named by their business name instead of first and last name.</summary>
    public string? BusinessName { get; set; }

    /// <summary>The name to show and to print: business name for companies, person name otherwise.</summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(BusinessName)
        ? BusinessName!
        : $"{FirstName} {LastName}".Trim();

    public IReadOnlyList<AddressDto> Addresses { get; set; } = [];
}

public class CreateCustomerRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>Optional (spec 002, FR-007).</summary>
    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>Defaults to retail when omitted (FR-007a).</summary>
    public Enums.CustomerType CustomerType { get; set; } = Enums.CustomerType.Retail;

    public AddressDto? Address { get; set; }

    public Enums.IdentificationType? IdentificationType { get; set; }

    public string? IdentificationNumber { get; set; }

    public string? BusinessName { get; set; }
}

public class UpdateCustomerRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public Enums.CustomerType CustomerType { get; set; } = Enums.CustomerType.Retail;

    public Enums.CustomerStatus Status { get; set; } = Enums.CustomerStatus.Active;

    public Enums.IdentificationType? IdentificationType { get; set; }

    public string? IdentificationNumber { get; set; }

    public string? BusinessName { get; set; }
}
