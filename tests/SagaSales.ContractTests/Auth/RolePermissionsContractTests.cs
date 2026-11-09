using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Auth;

/// <summary>
/// Spec 005 through the gateway: the owner creates a cashier, the cashier sells but cannot refund,
/// change prices, see analytics or manage users; nothing answers without a session.
/// Requires <c>SAGASALES_GATEWAY_URL</c> and a running stack.
/// </summary>
public class RolePermissionsContractTests
{
    private const string CashierPassword = "Cajera12345!";

    [Fact]
    public async Task A_cashier_sells_but_cannot_do_owner_or_manager_work()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_cashier_sells_but_cannot_do_owner_or_manager_work)))
        {
            return;
        }

        using var owner = await ContractSession.CreateAsync(withBranch: true);
        var product = await CreateProductAsync(owner);
        var (cashierUser, cashierEmail) = await CreateCashierAsync(owner);
        Assert.Equal("sales", cashierUser.Role);

        using var cashier = await SignInAsync(cashierEmail, owner.TenantId);

        // Allowed: selling.
        using var sale = await cashier.SendAsync(Request(HttpMethod.Post, "api/orders", owner.TenantId, new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            ShippingAddress = "Mostrador",
            Items = [new OrderItemRequest { ProductId = product.Id, Quantity = 1 }],
        }));
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);

        // Forbidden: refund, prices, analytics, users.
        using var refund = await cashier.SendAsync(Request(HttpMethod.Post, $"api/payments/{Guid.NewGuid()}/refund", owner.TenantId, new { amount = 1m }));
        using var price = await cashier.SendAsync(Request(HttpMethod.Put, $"api/products/{product.Id}/prices/wholesale", owner.TenantId, new SetPriceRequest { Price = 0.10m }));
        using var analytics = await cashier.SendAsync(Request(HttpMethod.Get, "api/analytics/sales-summary", owner.TenantId));
        using var users = await cashier.SendAsync(Request(HttpMethod.Get, "api/users", owner.TenantId));

        Assert.Equal(HttpStatusCode.Forbidden, refund.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, price.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, analytics.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_user_cannot_sign_in()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_deactivated_user_cannot_sign_in)))
        {
            return;
        }

        using var owner = await ContractSession.CreateAsync();
        var (cashier, email) = await CreateCashierAsync(owner);

        using var update = await owner.SendAsync(HttpMethod.Put, $"api/users/{cashier.Id}", new UpdateUserRequest
        {
            DisplayName = cashier.DisplayName,
            Role = "sales",
            IsActive = false,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var client = Gateway.CreateClient();
        using var login = await client.PostAsJsonAsync("api/auth/login", new LoginRequest { Email = email, Password = CashierPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task The_owner_cannot_deactivate_the_last_owner()
    {
        if (Gateway.TryAnnounceSkip(nameof(The_owner_cannot_deactivate_the_last_owner)))
        {
            return;
        }

        using var owner = await ContractSession.CreateAsync();
        using var list = await owner.SendAsync(HttpMethod.Get, "api/users");
        var users = await list.Content.ReadFromJsonAsync<List<UserDto>>();
        var me = users!.Single(u => u.Role == "admin");

        using var update = await owner.SendAsync(HttpMethod.Put, $"api/users/{me.Id}", new UpdateUserRequest
        {
            DisplayName = me.DisplayName,
            Role = "admin",
            IsActive = false,
        });

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Fact]
    public async Task Business_endpoints_do_not_answer_without_a_session()
    {
        if (Gateway.TryAnnounceSkip(nameof(Business_endpoints_do_not_answer_without_a_session)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        using var reserve = await client.PostAsJsonAsync($"api/products/{Guid.NewGuid()}/stock/reserve", new { quantity = 1 });
        using var users = await client.GetAsync("api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, reserve.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, users.StatusCode);
    }

    private static async Task<(UserDto User, string Email)> CreateCashierAsync(ContractSession owner)
    {
        var email = $"cajera-{Guid.NewGuid():N}@contract.local";
        using var response = await owner.SendAsync(HttpMethod.Post, "api/users", new CreateUserRequest
        {
            Email = email,
            DisplayName = "María Cajera",
            Role = "sales",
            Password = CashierPassword,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);
        return (user!, email);
    }

    private static async Task<HttpClient> SignInAsync(string email, Guid tenantId)
    {
        var client = Gateway.CreateClient();
        using var login = await client.PostAsJsonAsync("api/auth/login", new LoginRequest { Email = email, Password = CashierPassword });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Equal(tenantId, body!.User!.TenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        return client;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, Guid tenantId, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task<ProductDto> CreateProductAsync(ContractSession owner)
    {
        using var response = await owner.SendAsync(HttpMethod.Post, "api/products", new CreateProductRequest
        {
            Name = "Producto de cajera",
            Price = 0.40m,
            StockQuantity = 10,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }
}
