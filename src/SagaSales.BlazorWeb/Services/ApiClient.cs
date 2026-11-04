using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SagaSales.BlazorWeb.Services;

/// <summary>
/// Single HTTP entry point to the gateway. Replaces the previous per-page ad-hoc calls and
/// the non-existent <c>ApiClient</c> wiring.
/// </summary>
public sealed class ApiClient(HttpClient http, TokenStore store)
{
    // ── auth ────────────────────────────────────────────────────────────────
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (login is null)
        {
            return null;
        }

        await store.SetTokenAsync(login.AccessToken);

        if (login.User is not null)
        {
            await store.SetTenantIdAsync(login.User.TenantId.ToString());
        }

        return login;
    }

    public async Task<RegisterTenantResponse?> RegisterTenantAsync(RegisterTenantRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/register-tenant", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RegisterTenantResponse>()
            : null;
    }

    // ── catalog ─────────────────────────────────────────────────────────────
    public Task<PagedResult<ProductDto>?> GetProductsAsync(int pageNumber, int pageSize, string? search = null)
        => GetAsync<PagedResult<ProductDto>>(
            $"api/products?pageNumber={pageNumber}&pageSize={pageSize}&search={Uri.EscapeDataString(search ?? string.Empty)}");

    public Task<ProductDto?> CreateProductAsync(CreateProductRequest request)
        => PostAsync<ProductDto>("api/products", request);

    public Task<CategoryDto?> CreateCategoryAsync(CreateCategoryRequest request)
        => PostAsync<CategoryDto>("api/categories", request);

    // ── customers ───────────────────────────────────────────────────────────
    public Task<PagedResult<CustomerDto>?> GetCustomersAsync(int pageNumber, int pageSize, string? search = null)
        => GetAsync<PagedResult<CustomerDto>>(
            $"api/customers?pageNumber={pageNumber}&pageSize={pageSize}&search={Uri.EscapeDataString(search ?? string.Empty)}");

    public Task<CustomerDto?> CreateCustomerAsync(CreateCustomerRequest request)
        => PostAsync<CustomerDto>("api/customers", request);

    // ── orders ──────────────────────────────────────────────────────────────
    public Task<PagedResult<OrderDto>?> GetOrdersAsync(int pageNumber, int pageSize)
        => GetAsync<PagedResult<OrderDto>>($"api/orders?pageNumber={pageNumber}&pageSize={pageSize}");

    public Task<OrderDto?> CreateOrderAsync(CreateOrderRequest request)
        => PostAsync<OrderDto>("api/orders", request);

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status)
    {
        var request = await BuildAsync(HttpMethod.Put, $"api/orders/{orderId}/status", new UpdateOrderStatusRequest { NewStatus = (int)status });
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    // ── analytics ───────────────────────────────────────────────────────────
    public Task<SalesSummaryDto?> GetSalesSummaryAsync(DateOnly? from = null, DateOnly? to = null)
    {
        var query = $"api/analytics/sales-summary?from={from?.ToString("yyyy-MM-dd")}&to={to?.ToString("yyyy-MM-dd")}";
        return GetAsync<SalesSummaryDto>(query);
    }

    // ── helpers ─────────────────────────────────────────────────────────────
    private async Task<T?> GetAsync<T>(string path)
    {
        var request = await BuildAsync(HttpMethod.Get, path, null);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default;
    }

    private async Task<T?> PostAsync<T>(string path, object body)
    {
        var request = await BuildAsync(HttpMethod.Post, path, body);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default;
    }

    private async Task<HttpRequestMessage> BuildAsync(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);

        var token = await store.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var tenantId = await store.GetTenantIdAsync();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            request.Headers.TryAddWithoutValidation(SagaSales.Shared.Constants.SagaSalesConstants.Headers.TenantId, tenantId);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
