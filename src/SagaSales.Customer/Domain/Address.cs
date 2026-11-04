namespace SagaSales.Customer.Domain;

public class Address
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public string Line1 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string StateProvince { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public Customer? Customer { get; set; }
}
