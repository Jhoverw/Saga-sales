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

    /// <summary>
    /// Returns the created store, or the reason it failed. The reason matters: without it the panel
    /// can only say "revisa los datos", which hides a 409 or a validation error.
    /// </summary>
    public async Task<(RegisterTenantResponse? Response, string? Error)> RegisterTenantAsync(RegisterTenantRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/register-tenant", request);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(), null);
        }

        var body = await response.Content.ReadAsStringAsync();
        return (null, $"{(int)response.StatusCode} · {body}");
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
            $"api/customers?pageNumber={pageNumber}&pageSize={pageSize}&includeInactive=true&search={Uri.EscapeDataString(search ?? string.Empty)}");

    public Task<CustomerDto?> GetCustomerAsync(Guid id) => GetAsync<CustomerDto>($"api/customers/{id}");

    public Task<CustomerDto?> CreateCustomerAsync(CreateCustomerRequest request)
        => PostAsync<CustomerDto>("api/customers", request);

    public async Task<CustomerDto?> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/customers/{id}", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CustomerDto>()
            : null;
    }

    public async Task<bool> DeactivateCustomerAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/customers/{id}");
        return response.IsSuccessStatusCode;
    }

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

    public Task<OrderDto?> GetOrderAsync(Guid orderId) => GetAsync<OrderDto>($"api/orders/{orderId}");

    /// <summary>Cancels the order and returns its stock to the branch it came from.</summary>
    public async Task<bool> CancelOrderAsync(Guid orderId)
    {
        var request = await BuildAsync(HttpMethod.Post, $"api/orders/{orderId}/cancel", null);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    // ── payments ────────────────────────────────────────────────────────────
    /// <summary>
    /// Records a charge received outside the platform. This is the real entry point: the Payment
    /// service stores it and tells Sales, so the order reflects the money in one step.
    /// </summary>
    public Task<PaymentDto?> CreatePaymentAsync(ManualPaymentRequest request)
        => PostAsync<PaymentDto>("api/payments", request);

    public Task<IReadOnlyList<PaymentDto>?> GetPaymentsByOrderAsync(Guid orderId)
        => GetAsync<IReadOnlyList<PaymentDto>>($"api/payments?orderId={orderId}");

    public async Task<bool> RefundPaymentAsync(Guid paymentId, decimal amount)
    {
        var request = await BuildAsync(
            HttpMethod.Post,
            $"api/payments/{paymentId}/refund",
            new RefundPaymentRequest { RefundAmount = amount });

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

    // ── branches and settings (E-13) ────────────────────────────────────────
    public Task<List<BranchDto>?> GetBranchesAsync() => GetAsync<List<BranchDto>>("api/branches");

    public Task<BranchDto?> CreateBranchAsync(CreateBranchRequest request)
        => PostAsync<BranchDto>("api/branches", request);

    public Task<SettingsDto?> GetSettingsAsync() => GetAsync<SettingsDto>("api/settings");

    public async Task<SettingsDto?> UpdateSettingsAsync(UpdateSettingsRequest request)
    {
        var response = await http.PutAsJsonAsync("api/settings", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SettingsDto>()
            : null;
    }

    // ── suppliers and purchases (E-12) ──────────────────────────────────────
    public Task<List<SupplierDto>?> GetSuppliersAsync(bool includeInactive = false)
        => GetAsync<List<SupplierDto>>($"api/suppliers?includeInactive={includeInactive.ToString().ToLowerInvariant()}");

    public Task<SupplierDto?> CreateSupplierAsync(CreateSupplierRequest request)
        => PostAsync<SupplierDto>("api/suppliers", request);

    public async Task<SupplierDto?> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/suppliers/{id}", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SupplierDto>()
            : null;
    }

    public async Task<bool> DeactivateSupplierAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/suppliers/{id}");
        return response.IsSuccessStatusCode;
    }

    public Task<PagedResult<PurchaseDto>?> GetPurchasesAsync(int pageNumber = 1, int pageSize = 20)
        => GetAsync<PagedResult<PurchaseDto>>($"api/purchases?pageNumber={pageNumber}&pageSize={pageSize}");

    public async Task<PurchaseDto?> CreatePurchaseAsync(CreatePurchaseRequest request)
    {
        var response = await http.PostAsJsonAsync("api/purchases", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PurchaseDto>()
            : null;
    }

    public async Task<PurchaseDto?> ReceivePurchaseAsync(Guid id)
    {
        var response = await http.PostAsync($"api/purchases/{id}/receive", null);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PurchaseDto>()
            : null;
    }

    // ── inventory (E-13) ────────────────────────────────────────────────────
    public Task<List<StockByBranchDto>?> GetStockByBranchAsync(Guid productId)
        => GetAsync<List<StockByBranchDto>>($"api/inventory/products/{productId}/branches");

    public async Task<bool> SetLocationAsync(Guid productId, SetLocationRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/inventory/products/{productId}/location", request);
        return response.IsSuccessStatusCode;
    }

    public Task<List<ExpiringBatchDto>?> GetExpiringAsync(int maxRows = 50)
        => GetAsync<List<ExpiringBatchDto>>($"api/inventory/expiring?maxRows={maxRows}");
}
