using System.Net;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;
using Xunit;

namespace SagaSales.ContractTests.Catalog;

/// <summary>
/// Contract of the Catalog service (contracts/CatalogApi.cs): products, the base price and the
/// price per customer type. Requires <c>SAGASALES_GATEWAY_URL</c> and a running stack.
/// </summary>
public class CatalogContractTests
{
    [Fact]
    public async Task Price_endpoint_returns_the_list_price_and_reports_the_fallback()
    {
        if (Gateway.TryAnnounceSkip(nameof(Price_endpoint_returns_the_list_price_and_reports_the_fallback)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);

        var product = await CreateProductAsync(client, session, "Laptop Pro", 100m);

        // No wholesale price yet: the base price applies and the source says so.
        var fallback = await GetQuoteAsync(client, session, product.Id, CustomerType.Wholesale);
        Assert.Equal(100m, fallback.Price);
        Assert.Equal(PriceSource.Base, fallback.Source);

        // Set the wholesale price and read both quotes.
        using var set = await client.SendAsync(
            Authorized(HttpMethod.Put, $"api/products/{product.Id}/prices/wholesale", session, new SetPriceRequest { Price = 80m }));

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        var wholesale = await GetQuoteAsync(client, session, product.Id, CustomerType.Wholesale);
        var retail = await GetQuoteAsync(client, session, product.Id, CustomerType.Retail);

        Assert.Equal(80m, wholesale.Price);
        Assert.Equal(PriceSource.List, wholesale.Source);

        Assert.Equal(100m, retail.Price);
        Assert.Equal(PriceSource.Base, retail.Source);
    }

    [Fact]
    public async Task Product_list_is_paged_and_filters_by_search()
    {
        if (Gateway.TryAnnounceSkip(nameof(Product_list_is_paged_and_filters_by_search)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);

        for (var i = 1; i <= 3; i++)
        {
            await CreateProductAsync(client, session, $"Widget {i}", 10m * i);
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "api/products?pageNumber=1&pageSize=2&search=Widget");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", session.TenantId.ToString());

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        Assert.NotNull(page);
        Assert.Equal(2, page!.Items.Count);
        Assert.Equal(3, page.TotalCount);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task Creating_a_product_with_a_one_character_name_is_rejected()
    {
        if (Gateway.TryAnnounceSkip(nameof(Creating_a_product_with_a_one_character_name_is_rejected)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);

        using var response = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/products", session, new CreateProductRequest
            {
                Name = "X",
                Price = 10m,
                StockQuantity = 1,
                Currency = "USD",
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<ProductDto> CreateProductAsync(
        HttpClient client,
        SignedInStore session,
        string name,
        decimal price)
    {
        using var response = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/products", session, new CreateProductRequest
            {
                Name = name,
                Price = price,
                StockQuantity = 10,
                Currency = "USD",
            }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(product);
        return product!;
    }

    private static async Task<PriceQuoteDto> GetQuoteAsync(
        HttpClient client,
        SignedInStore session,
        Guid productId,
        CustomerType customerType)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/products/{productId}/price?customerType={customerType.ToString().ToLowerInvariant()}");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", session.TenantId.ToString());

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var quote = await response.Content.ReadFromJsonAsync<PriceQuoteDto>();
        Assert.NotNull(quote);
        return quote!;
    }

    private static HttpRequestMessage Authorized<T>(HttpMethod method, string path, SignedInStore session, T body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", session.TenantId.ToString());
        return request;
    }
}

/// <summary>Registers a throwaway store and signs in, for the contract tests that need a tenant.</summary>
internal sealed record SignedInStore(string Token, Guid TenantId)
{
    public static async Task<SignedInStore> CreateAsync(HttpClient client)
    {
        var slug = "ct-" + Guid.NewGuid().ToString("N")[..20];
        var email = $"{slug}@contract.local";
        const string Password = "Demo12345!";

        using var register = await client.PostAsJsonAsync("api/auth/register-tenant", new RegisterTenantRequest
        {
            Slug = slug,
            DisplayName = "Catalog contract store",
            AdminEmail = email,
            AdminPassword = Password,
        });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await client.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password,
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var session = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        Assert.NotNull(session!.User);

        // A store without a branch cannot hold or sell stock; the real flow creates one first.
        using var branch = new HttpRequestMessage(HttpMethod.Post, "api/branches")
        {
            Content = JsonContent.Create(new CreateBranchRequest { Name = "Principal", IsDefault = true }),
        };
        branch.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);
        branch.Headers.TryAddWithoutValidation("X-Tenant-Id", session.User.TenantId.ToString());

        using var branchResponse = await client.SendAsync(branch);
        Assert.Equal(HttpStatusCode.Created, branchResponse.StatusCode);

        // Orders need a VAT rate in force (V-03). A 15 % rate open since 2000 keeps every order date covered.
        using var tax = new HttpRequestMessage(HttpMethod.Post, "api/taxes")
        {
            Content = JsonContent.Create(new CreateTaxRateRequest(
                0.15m,
                new DateOnly(2000, 1, 1),
                null,
                "IVA contrato")),
        };
        tax.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);
        tax.Headers.TryAddWithoutValidation("X-Tenant-Id", session.User.TenantId.ToString());

        using var taxResponse = await client.SendAsync(tax);
        Assert.Equal(HttpStatusCode.OK, taxResponse.StatusCode);

        return new SignedInStore(session.AccessToken, session.User.TenantId);
    }
}
