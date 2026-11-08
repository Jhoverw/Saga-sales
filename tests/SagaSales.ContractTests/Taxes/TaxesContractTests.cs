using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Taxes;

/// <summary>
/// Contract of the tax configuration (IVA periods) through the gateway. Requires
/// <c>SAGASALES_GATEWAY_URL</c> and a running stack.
/// </summary>
public class TaxesContractTests
{
    [Fact]
    public async Task Listing_rates_without_a_token_returns_401()
    {
        if (Gateway.TryAnnounceSkip(nameof(Listing_rates_without_a_token_returns_401)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        using var response = await client.GetAsync("api/taxes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_created_rate_is_listed_with_its_period()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_created_rate_is_listed_with_its_period)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var token = await RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var description = "IVA contract " + Guid.NewGuid().ToString("N")[..8];
        var request = new CreateTaxRateRequest(
            RatePercentage: 0.16m,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: null,
            Description: description);

        using var created = await client.PostAsJsonAsync("api/taxes", request);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var rates = await client.GetFromJsonAsync<List<TaxRateDto>>("api/taxes");
        Assert.NotNull(rates);
        Assert.Contains(rates!, r => r.Description == description && r.RatePercentage == 0.16m);
    }

    [Fact]
    public async Task Rates_of_one_store_are_invisible_to_another()
    {
        if (Gateway.TryAnnounceSkip(nameof(Rates_of_one_store_are_invisible_to_another)))
        {
            return;
        }

        using var clientA = Gateway.CreateClient();
        using var clientB = Gateway.CreateClient();

        var tokenA = await RegisterAndLoginAsync(clientA);
        var tokenB = await RegisterAndLoginAsync(clientB);

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var description = "IVA aislada " + Guid.NewGuid().ToString("N")[..8];
        var request = new CreateTaxRateRequest(
            RatePercentage: 0.16m,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: null,
            Description: description);

        using var created = await clientA.PostAsJsonAsync("api/taxes", request);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var ratesA = await clientA.GetFromJsonAsync<List<TaxRateDto>>("api/taxes");
        var ratesB = await clientB.GetFromJsonAsync<List<TaxRateDto>>("api/taxes");

        Assert.NotNull(ratesA);
        Assert.NotNull(ratesB);
        Assert.Contains(ratesA!, r => r.Description == description);
        Assert.DoesNotContain(ratesB!, r => r.Description == description);
    }

    [Fact]
    public async Task An_invalid_percentage_returns_400()
    {
        if (Gateway.TryAnnounceSkip(nameof(An_invalid_percentage_returns_400)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var token = await RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateTaxRateRequest(
            RatePercentage: 15m, // 1500 %: the API expects a fraction
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: null,
            Description: "invalid");

        using var created = await client.PostAsJsonAsync("api/taxes", request);
        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client)
    {
        var slug = "tax-ct-" + Guid.NewGuid().ToString("N")[..12];
        var email = $"{slug}@contract.local";
        const string Password = "Demo12345!";

        using var created = await client.PostAsJsonAsync("api/auth/register-tenant", new RegisterTenantRequest
        {
            Slug = slug,
            DisplayName = "Tax contract store",
            AdminEmail = email,
            AdminPassword = Password,
        });
        created.EnsureSuccessStatusCode();

        using var login = await client.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password,
        });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }
}
