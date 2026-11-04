namespace SagaSales.Customer.Domain;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public IList<Address> Addresses { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}".Trim();
}
