// Customer Service — REST contract.
// Declarative contract: this file documents the API, it is not compiled.
// Procedures in data-model.md §7.

namespace SagaSales.Contracts;

public class CreateCustomerRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }

    /// <summary>Defaults to retail when omitted (D5, FR-007a).</summary>
    public CustomerType CustomerType { get; set; } = CustomerType.Retail;

    /// <summary>Optional at creation.</summary>
    public AddressDto? Address { get; set; }
}

public class UpdateCustomerRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public CustomerType CustomerType { get; set; } = CustomerType.Retail;
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
}

public class CustomerDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public CustomerType CustomerType { get; set; }
    public CustomerStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<AddressDto> Addresses { get; set; } = [];
}

public class AddressDto
{
    public string Line1 { get; set; } = "";
    public string City { get; set; } = "";
    public string StateProvince { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
}

// HTTP Endpoints — bearer token + tenant required.
//
// POST   /api/customers        → 201 CustomerDto · 409 CONFLICT (email already used in this store)
// GET    /api/customers        → 200 PagedResult<CustomerDto>
//        ?pageNumber&pageSize&search     (email, phone or name — FR-020)
// GET    /api/customers/{id}   → 200 CustomerDto · 404
// PUT    /api/customers/{id}   → 200 CustomerDto · 404
// DELETE /api/customers/{id}   → 204 (soft delete: stops appearing, keeps order history — FR-021)
//
// Invariants
//  - Email is unique inside a store and may repeat across stores (FR-020).
//  - The same email in another store must succeed, not be rejected.
//  - Customer type drives which price list applies when that customer orders (D5).
//  - A deleted customer is not returned by lists but their orders keep their history.

// Errors: 400 · 401 · 403 · 404 · 409

// Domain events published
//   CustomerCreatedEvent
