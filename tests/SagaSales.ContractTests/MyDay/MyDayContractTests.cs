using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.MyDay;

/// <summary>
/// Spec 006 a través del gateway: el cajero ve solo sus ventas, registra caja chica dentro del tope y
/// no puede anular gastos ni cambiar el tope. Requiere <c>SAGASALES_GATEWAY_URL</c> y el stack levantado.
/// </summary>
public class MyDayContractTests
{
    private const string CashierPassword = "Cajera12345!";
    private const int Ecuador = -300;

    [Fact]
    public async Task A_cashier_sees_only_their_own_sales()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_cashier_sees_only_their_own_sales)))
        {
            return;
        }

        using var owner = await ContractSession.CreateAsync(withBranch: true);
        var product = await CreateProductAsync(owner);
        var ownerId = await MyIdAsync(owner);
        var cashierEmail = await CreateCashierAsync(owner);
        using var cashier = await SignInAsync(cashierEmail);

        using (var ownerSale = await owner.SendAsync(HttpMethod.Post, "api/orders", Sale(product.Id, 2)))
        {
            Assert.Equal(HttpStatusCode.Created, ownerSale.StatusCode);
        }

        using (var cashierSale = await cashier.SendAsync(Request(HttpMethod.Post, "api/orders", owner.TenantId, Sale(product.Id, 1))))
        {
            Assert.True(
                cashierSale.StatusCode == HttpStatusCode.Created,
                $"Venta del cajero: {(int)cashierSale.StatusCode} {await cashierSale.Content.ReadAsStringAsync()}");
        }

        // Aunque pida las ventas del dueño, el cajero recibe solo las suyas (FR-005).
        using var mine = await cashier.SendAsync(Request(
            HttpMethod.Get, $"api/orders/seller-summary?days=15&offsetMinutes={Ecuador}&sellerId={ownerId}", owner.TenantId));
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        var summary = await mine.Content.ReadFromJsonAsync<SellerSalesSummaryDto>();
        Assert.Equal(15, summary!.Days.Count);
        Assert.Equal(1, summary.TodayOrders);
        Assert.Equal(1, Assert.Single(summary.TodayProducts).Quantity);
        Assert.Empty(summary.Sellers);

        using var store = await owner.SendAsync(HttpMethod.Get, $"api/orders/seller-summary?days=15&offsetMinutes={Ecuador}");
        var all = await store.Content.ReadFromJsonAsync<SellerSalesSummaryDto>();
        Assert.Equal(2, all!.TodayOrders);
        Assert.Equal(2, all.Sellers.Count);
    }

    [Fact]
    public async Task Petty_cash_respects_the_limit_and_only_managers_void_it()
    {
        if (Gateway.TryAnnounceSkip(nameof(Petty_cash_respects_the_limit_and_only_managers_void_it)))
        {
            return;
        }

        using var owner = await ContractSession.CreateAsync();
        var cashierEmail = await CreateCashierAsync(owner);
        using var cashier = await SignInAsync(cashierEmail);

        using var created = await cashier.SendAsync(Request(HttpMethod.Post, "api/payments/petty-cash", owner.TenantId, Expense(5.00m)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var expense = await created.Content.ReadFromJsonAsync<PettyCashExpenseDto>();

        using var tooMuch = await cashier.SendAsync(Request(HttpMethod.Post, "api/payments/petty-cash", owner.TenantId, Expense(25.00m)));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);

        using var summary = await cashier.SendAsync(Request(HttpMethod.Get, $"api/payments/summary?offsetMinutes={Ecuador}", owner.TenantId));
        var day = await summary.Content.ReadFromJsonAsync<PaymentDaySummaryDto>();
        Assert.Equal(5.00m, day!.PettyCash);
        Assert.Equal(-5.00m, day.ExpectedCash);

        using var cashierVoid = await cashier.SendAsync(Request(HttpMethod.Post, $"api/payments/petty-cash/{expense!.Id}/void", owner.TenantId));
        using var cashierLimit = await cashier.SendAsync(Request(HttpMethod.Put, "api/payments/petty-cash/settings", owner.TenantId, new PettyCashSettingsDto { MaxAmount = 500m }));
        Assert.Equal(HttpStatusCode.Forbidden, cashierVoid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cashierLimit.StatusCode);

        using var ownerVoid = await owner.SendAsync(HttpMethod.Post, $"api/payments/petty-cash/{expense.Id}/void");
        Assert.Equal(HttpStatusCode.NoContent, ownerVoid.StatusCode);

        using var ownerLimit = await owner.SendAsync(HttpMethod.Put, "api/payments/petty-cash/settings", new PettyCashSettingsDto { MaxAmount = 30m });
        Assert.Equal(HttpStatusCode.OK, ownerLimit.StatusCode);

        using var nowAllowed = await cashier.SendAsync(Request(HttpMethod.Post, "api/payments/petty-cash", owner.TenantId, Expense(25.00m)));
        Assert.Equal(HttpStatusCode.Created, nowAllowed.StatusCode);
    }

    [Fact]
    public async Task A_cashier_sees_inventory_alerts()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_cashier_sees_inventory_alerts)))
        {
            return;
        }

        using var owner = await ContractSession.CreateAsync(withBranch: true);
        var cashierEmail = await CreateCashierAsync(owner);
        using var cashier = await SignInAsync(cashierEmail);

        using var alerts = await cashier.SendAsync(Request(HttpMethod.Get, "api/inventory/alerts", owner.TenantId));
        Assert.Equal(HttpStatusCode.OK, alerts.StatusCode);
        Assert.NotNull(await alerts.Content.ReadFromJsonAsync<InventoryAlertsDto>());
    }

    private static CreateOrderRequest Sale(Guid productId, int quantity) => new()
    {
        CustomerId = Guid.NewGuid(),
        ShippingAddress = "Mostrador",
        Items = [new OrderItemRequest { ProductId = productId, Quantity = quantity }],
    };

    private static CreatePettyCashExpenseRequest Expense(decimal amount) => new()
    {
        Category = PettyCashCategory.Maintenance,
        Description = "Foco del baño",
        Amount = amount,
    };

    private static async Task<Guid> MyIdAsync(ContractSession owner)
    {
        using var list = await owner.SendAsync(HttpMethod.Get, "api/users");
        var users = await list.Content.ReadFromJsonAsync<List<UserDto>>();
        return users!.Single(u => u.Role == "admin").Id;
    }

    private static async Task<string> CreateCashierAsync(ContractSession owner)
    {
        var email = $"cajero-{Guid.NewGuid():N}@contract.local";
        using var response = await owner.SendAsync(HttpMethod.Post, "api/users", new CreateUserRequest
        {
            Email = email,
            DisplayName = "Pedro Cajero",
            Role = "sales",
            Password = CashierPassword,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }

    private static async Task<HttpClient> SignInAsync(string email)
    {
        var client = Gateway.CreateClient();
        using var login = await client.PostAsJsonAsync("api/auth/login", new LoginRequest { Email = email, Password = CashierPassword });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
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
            Name = "Producto panel",
            Price = 0.40m,
            StockQuantity = 10,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }
}
