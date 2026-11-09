using System.Net;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;
using Xunit;

namespace SagaSales.ContractTests.Customers;

/// <summary>
/// Spec 002: customers with SRI identification and optional email, through the gateway.
/// Requires <c>SAGASALES_GATEWAY_URL</c> and a running stack. Identification numbers are synthetic.
/// </summary>
public class CustomerIdentificationContractTests
{
    [Fact]
    public async Task A_cedula_with_a_wrong_check_digit_is_rejected_with_400()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_cedula_with_a_wrong_check_digit_is_rejected_with_400)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync();

        using var response = await session.SendAsync(HttpMethod.Post, "api/customers", new CreateCustomerRequest
        {
            FirstName = "Ana",
            LastName = "Pérez",
            IdentificationType = IdentificationType.Cedula,
            IdentificationNumber = "1700000002",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_customer_without_email_is_created_with_its_identification()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_customer_without_email_is_created_with_its_identification)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync();

        using var response = await session.SendAsync(HttpMethod.Post, "api/customers", new CreateCustomerRequest
        {
            FirstName = "Ana",
            LastName = "Pérez",
            IdentificationType = IdentificationType.Cedula,
            IdentificationNumber = "1700000001",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(customer);
        Assert.Null(customer!.Email);
        Assert.Equal(IdentificationType.Cedula, customer.IdentificationType);
        Assert.Equal("1700000001", customer.IdentificationNumber);
    }

    [Fact]
    public async Task The_same_identification_twice_in_a_store_returns_409()
    {
        if (Gateway.TryAnnounceSkip(nameof(The_same_identification_twice_in_a_store_returns_409)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync();
        var request = new CreateCustomerRequest
        {
            BusinessName = "Comercial Andina S.A.",
            IdentificationType = IdentificationType.Ruc,
            IdentificationNumber = "1790012345001",
        };

        using var first = await session.SendAsync(HttpMethod.Post, "api/customers", request);
        using var second = await session.SendAsync(HttpMethod.Post, "api/customers", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_company_ruc_without_business_name_is_rejected_with_400()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_company_ruc_without_business_name_is_rejected_with_400)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync();

        using var response = await session.SendAsync(HttpMethod.Post, "api/customers", new CreateCustomerRequest
        {
            FirstName = "Juan",
            LastName = "Torres",
            IdentificationType = IdentificationType.Ruc,
            IdentificationNumber = "1790012345001",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_can_be_searched_by_identification()
    {
        if (Gateway.TryAnnounceSkip(nameof(Customers_can_be_searched_by_identification)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync();
        using var created = await session.SendAsync(HttpMethod.Post, "api/customers", new CreateCustomerRequest
        {
            FirstName = "Luis",
            LastName = "Mora",
            IdentificationType = IdentificationType.Cedula,
            IdentificationNumber = "0912345675",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var response = await session.SendAsync(HttpMethod.Get, "api/customers?search=09123456");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<CustomerDto>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, c => c.IdentificationNumber == "0912345675");
    }
}
