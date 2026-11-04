// Customer Endpoints — Customer Service Contract

namespace SagaSales.Contracts;

public class CreateCustomerRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public AddressDto Address { get; set; } = default!;
}

public class CustomerDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Status { get; set; }
    public AddressDto? Address { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddressDto
{
    public string Line1 { get; set; } = "";
    public string City { get; set; } = "";
    public string StateProvince { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
}

// HTTP Endpoints
// POST   /api/customers       → CreateCustomer (201 CustomerDto)
// GET    /api/customers       → ListCustomers (paged, search by email/phone)
// GET    /api/customers/{id}  → GetCustomer
// PUT    /api/customers/{id}  → UpdateCustomer (includes address update)
// DELETE /api/customers/{id}  → DeleteCustomer (soft delete)
