namespace SagaSales.Customer.Mapping;

using SagaSales.Customer.Domain;

public static class CustomerMapping
{
    public static CustomerDto ToDto(this Customer customer) => new()
    {
        Id = customer.Id,
        FirstName = customer.FirstName,
        LastName = customer.LastName,
        Email = customer.Email,
        Phone = customer.Phone,
        Status = customer.Status,
        CreatedAt = customer.CreatedAt,
        Addresses = customer.Addresses.Select(a => a.ToDto()).ToList(),
    };

    public static AddressDto ToDto(this Address address) => new()
    {
        Line1 = address.Line1,
        City = address.City,
        StateProvince = address.StateProvince,
        PostalCode = address.PostalCode,
        Country = address.Country,
    };
}
