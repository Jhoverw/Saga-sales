using System.Net.Http.Headers;
using System.Net.Http.Json;
using SagaSales.BlazorWeb.DTOs;

namespace SagaSales.BlazorWeb.Services;

/// <summary>
/// Single HTTP entry point to the gateway. Replaces the previous per-page ad-hoc calls and
/// the non-existent <c>ApiClient</c> wiring.
/// </summary>
public sealed class ApiClient(HttpClient http, TokenStore store)
{
    // Every date shown in the panel is local time (Ecuador, GMT−5); see LocalDateTimeConverter.
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new SagaSales.Shared.Time.LocalDateTimeConverter() },
    };

    // ── auth ────────────────────────────────────────────────────────────────
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request, Json);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
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
        var response = await http.PostAsJsonAsync("api/auth/register-tenant", request, Json);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(Json), null);
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

    /// <summary>Uploads (or replaces) the product's picture and returns the updated product.</summary>
    public async Task<ProductDto?> UploadProductImageAsync(Guid productId, byte[] bytes, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, $"api/products/{productId}/image");
        await ApplyHeadersAsync(request);
        request.Content = content;

        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProductDto>(Json) : null;
    }

    /// <summary>Turns the stored relative path (e.g. <c>images/products/x.png</c>) into a full URL.</summary>
    public string? ImageUrl(string? path)
        => string.IsNullOrWhiteSpace(path)
            ? null
            : (http.BaseAddress is null ? path : new Uri(http.BaseAddress, path).ToString());

    public Task<CategoryDto?> CreateCategoryAsync(CreateCategoryRequest request)
        => PostAsync<CategoryDto>("api/categories", request);

    // ── customers ───────────────────────────────────────────────────────────
    public Task<PagedResult<CustomerDto>?> GetCustomersAsync(int pageNumber, int pageSize, string? search = null)
        => GetAsync<PagedResult<CustomerDto>>(
            $"api/customers?pageNumber={pageNumber}&pageSize={pageSize}&includeInactive=true&search={Uri.EscapeDataString(search ?? string.Empty)}");

    public Task<CustomerDto?> GetCustomerAsync(Guid id) => GetAsync<CustomerDto>($"api/customers/{id}");

    public Task<CustomerDto?> CreateCustomerAsync(CreateCustomerRequest request)
        => PostAsync<CustomerDto>("api/customers", request);

    /// <summary>
    /// Creates a customer and keeps the reason when it fails, so Caja can tell "already exists" (409)
    /// from "invalid data" (400) instead of a generic error (spec 002, US3).
    /// </summary>
    public async Task<(CustomerDto? Customer, System.Net.HttpStatusCode? Status, string? Error)> CreateCustomerWithResultAsync(
        CreateCustomerRequest request)
    {
        var message = await BuildAsync(HttpMethod.Post, "api/customers", request);
        var response = await http.SendAsync(message);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<CustomerDto>(Json), response.StatusCode, null);
        }

        var body = await response.Content.ReadAsStringAsync();
        return (null, response.StatusCode, ExtractError(body));
    }

    private static string ExtractError(string body)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            foreach (var name in new[] { "message", "detail", "title" })
            {
                if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                    && document.RootElement.TryGetProperty(name, out var value)
                    && value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return value.GetString() ?? body;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Not JSON: fall through and show the raw text.
        }

        return body.Length > 200 ? body[..200] : body;
    }

    public async Task<CustomerDto?> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/customers/{id}", request, Json);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CustomerDto>(Json)
            : null;
    }

    public async Task<bool> DeactivateCustomerAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/customers/{id}");
        return response.IsSuccessStatusCode;
    }

    // ── orders ──────────────────────────────────────────────────────────────
    public Task<PagedResult<OrderDto>?> GetOrdersAsync(int pageNumber, int pageSize, OrderStatus? status = null)
    {
        var query = $"api/orders?pageNumber={pageNumber}&pageSize={pageSize}";
        if (status is not null)
        {
            query += $"&status={(int)status.Value}";
        }

        return GetAsync<PagedResult<OrderDto>>(query);
    }

    public Task<OrderDto?> CreateOrderAsync(CreateOrderRequest request)
        => PostAsync<OrderDto>("api/orders", request);

    /// <summary>Cambia el estado; al pasar a Enviado lleva el envío (spec 007). Devuelve el motivo si se rechaza.</summary>
    public async Task<(bool Ok, string? Error)> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, ShipmentRequest? shipment = null)
    {
        var request = await BuildAsync(
            HttpMethod.Put,
            $"api/orders/{orderId}/status",
            new UpdateOrderStatusRequest { NewStatus = (int)status, Shipment = shipment });
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? (true, null)
            : (false, ExtractError(await response.Content.ReadAsStringAsync()));
    }

    public Task<OrderShipmentDto?> GetOrderShipmentAsync(Guid orderId)
        => GetAsync<OrderShipmentDto>($"api/orders/{orderId}/shipment");

    // ── pedidos (spec 008) ──────────────────────────────────────────────────
    public Task<OrderFulfillmentDto?> GetOrderFulfillmentAsync(Guid orderId)
        => GetAsync<OrderFulfillmentDto>($"api/orders/{orderId}/fulfillment");

    public Task<List<RemoteOrderDto>?> GetRemoteOrdersAsync() => GetAsync<List<RemoteOrderDto>>("api/orders/remote");

    public Task<SalesSettingsDto?> GetSalesSettingsAsync() => GetAsync<SalesSettingsDto>("api/orders/settings");

    public Task<(SalesSettingsDto? Value, System.Net.HttpStatusCode? Status, string? Error)> SetSalesSettingsAsync(int hours)
        => SendForResultAsync<SalesSettingsDto>(HttpMethod.Put, "api/orders/settings", new SalesSettingsDto { OrderHoldHours = hours });

    public async Task<(bool Ok, string? Error)> VerifyPaymentAsync(Guid paymentId)
    {
        var message = await BuildAsync(HttpMethod.Post, $"api/payments/{paymentId}/verify", null);
        var response = await http.SendAsync(message);
        return response.IsSuccessStatusCode ? (true, null) : (false, ExtractError(await response.Content.ReadAsStringAsync()));
    }

    public async Task<(bool Ok, string? Error)> RejectPaymentAsync(Guid paymentId, string reason)
    {
        var message = await BuildAsync(HttpMethod.Post, $"api/payments/{paymentId}/reject", new RejectPaymentRequest { Reason = reason });
        var response = await http.SendAsync(message);
        return response.IsSuccessStatusCode ? (true, null) : (false, ExtractError(await response.Content.ReadAsStringAsync()));
    }

    // ── comprobantes de pago (spec 007) ─────────────────────────────────────
    public async Task<(PaymentAttachmentDto? Value, string? Error)> UploadPaymentAttachmentAsync(
        Guid paymentId, byte[] bytes, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, $"api/payments/{paymentId}/attachments");
        await ApplyHeadersAsync(request);
        request.Content = content;

        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<PaymentAttachmentDto>(Json), null)
            : (null, ExtractError(await response.Content.ReadAsStringAsync()));
    }

    public Task<List<PaymentAttachmentDto>?> GetPaymentAttachmentsAsync(Guid paymentId)
        => GetAsync<List<PaymentAttachmentDto>>($"api/payments/{paymentId}/attachments");

    /// <summary>Descarga el comprobante con la sesión del usuario: no hay URL pública.</summary>
    public async Task<(byte[] Content, string ContentType)?> DownloadPaymentAttachmentAsync(Guid attachmentId)
    {
        var request = await BuildAsync(HttpMethod.Get, $"api/payments/attachments/{attachmentId}", null);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var type = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return (bytes, type);
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
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(Json) : default;
    }

    private async Task<T?> PostAsync<T>(string path, object body)
    {
        var request = await BuildAsync(HttpMethod.Post, path, body);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(Json) : default;
    }

    private async Task<HttpRequestMessage> BuildAsync(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        await ApplyHeadersAsync(request);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return request;
    }

    private async Task ApplyHeadersAsync(HttpRequestMessage request)
    {
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
    }

    // ── product lookup (V‑01) ────────────────────────────────────────────────
    public Task<IReadOnlyList<ProductLookupDto>?> LookupProductsAsync(string term)
        => GetAsync<IReadOnlyList<ProductLookupDto>>("api/catalog/products/lookup?term=" + Uri.EscapeDataString(term));

    // ── pre‑sale (V‑02, V‑08) ────────────────────────────────────────────────
    public Task<PreSaleDto?> CreatePreSaleAsync(CreatePreSaleRequest request)
        => PostAsync<PreSaleDto>("api/sales/pre-sales", request);
    public Task<PreSaleDto?> GetPreSaleAsync(Guid id)
        => GetAsync<PreSaleDto>("api/sales/pre-sales/" + id);

    // ── tax (V‑03) ────────────────────────────────────────────────────────
    public Task<TaxRateDto?> GetCurrentTaxAsync(DateTime date)
        => GetAsync<TaxRateDto>($"api/taxes/current?date={date:yyyy-MM-dd}");

    // ── payments (V‑04, V‑05, V‑06) ────────────────────────────────────────
    public Task<PaymentDto?> PayCashAsync(CashPaymentDto request)
        => PostAsync<PaymentDto>("api/sales/payments/cash", request);
    public Task<PaymentDto?> PayCardAsync(CardPaymentDto request)
        => PostAsync<PaymentDto>("api/sales/payments/card", request);
    public Task<PaymentDto?> PayCreditAsync(CreditPaymentDto request)
        => PostAsync<PaymentDto>("api/sales/payments/credit", request);

    public Task<InvoiceResponseDto?> GetInvoiceAsync(Guid id)
        => GetAsync<InvoiceResponseDto>($"api/ecuador-invoicing/invoices/{id}");

    // ── invoice creation (Ecuador) ───────────────────────────────────────────────
    public Task<InvoiceResponseDto?> CreateInvoiceAsync(CreateInvoiceDto request)
        => PostAsync<InvoiceResponseDto>("api/ecuador-invoicing/invoices", request);

    // ── tax report (V‑09) ───────────────────────────────────────────────────────
    public Task<IReadOnlyList<TaxPeriodReportDto>?> GetIvaReportAsync(DateTime from, DateTime to)
        => GetAsync<IReadOnlyList<TaxPeriodReportDto>>($"api/reports/iva?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");


    public Task<List<TaxRateDto>?> GetTaxRatesAsync()
        => GetAsync<List<TaxRateDto>>("api/taxes");

    public async Task<int?> CreateTaxRateAsync(CreateTaxRateRequest request)
    {
        return await PostAsync<int>("api/taxes", request);
    }

    // ── branches and settings (E-13) ────────────────────────────────────────
    public Task<List<BranchDto>?> GetBranchesAsync(bool includeInactive = false)
        => GetAsync<List<BranchDto>>($"api/branches?includeInactive={includeInactive.ToString().ToLowerInvariant()}");

    public Task<BranchDto?> CreateBranchAsync(CreateBranchRequest request)
        => PostAsync<BranchDto>("api/branches", request);

    public async Task<BranchDto?> UpdateBranchAsync(Guid id, UpdateBranchRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/branches/{id}", request, Json);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<BranchDto>(Json)
            : null;
    }

    public async Task<bool> DeactivateBranchAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/branches/{id}");
        return response.IsSuccessStatusCode;
    }

    public Task<SettingsDto?> GetSettingsAsync() => GetAsync<SettingsDto>("api/settings");

    public async Task<SettingsDto?> UpdateSettingsAsync(UpdateSettingsRequest request)
    {
        var response = await http.PutAsJsonAsync("api/settings", request, Json);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SettingsDto>(Json)
            : null;
    }

    // ── suppliers and purchases (E-12) ──────────────────────────────────────
    public Task<List<SupplierDto>?> GetSuppliersAsync(bool includeInactive = false)
        => GetAsync<List<SupplierDto>>($"api/suppliers?includeInactive={includeInactive.ToString().ToLowerInvariant()}");

    public Task<SupplierDto?> CreateSupplierAsync(CreateSupplierRequest request)
        => PostAsync<SupplierDto>("api/suppliers", request);

    public async Task<SupplierDto?> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/suppliers/{id}", request, Json);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SupplierDto>(Json)
            : null;
    }

    public async Task<bool> DeactivateSupplierAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/suppliers/{id}");
        return response.IsSuccessStatusCode;
    }

    public Task<PagedResult<PurchaseDto>?> GetPurchasesAsync(int pageNumber = 1, int pageSize = 20)
        => GetAsync<PagedResult<PurchaseDto>>($"api/purchases?pageNumber={pageNumber}&pageSize={pageSize}");

    /// <summary>One purchase with all its lines (the grouped records of the document).</summary>
    public Task<PurchaseDto?> GetPurchaseAsync(Guid id) => GetAsync<PurchaseDto>($"api/purchases/{id}");

    public async Task<PurchaseDto?> CreatePurchaseAsync(CreatePurchaseRequest request)
    {
        var response = await http.PostAsJsonAsync("api/purchases", request, Json);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PurchaseDto>(Json)
            : null;
    }

    public async Task<PurchaseDto?> ReceivePurchaseAsync(Guid id)
    {
        var response = await http.PostAsync($"api/purchases/{id}/receive", null);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PurchaseDto>(Json)
            : null;
    }

    // ── inventory (E-13) ────────────────────────────────────────────────────
    public Task<List<StockByBranchDto>?> GetStockByBranchAsync(Guid productId)
        => GetAsync<List<StockByBranchDto>>($"api/inventory/products/{productId}/branches");

    public async Task<bool> SetLocationAsync(Guid productId, SetLocationRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/inventory/products/{productId}/location", request, Json);
        return response.IsSuccessStatusCode;
    }

    public Task<List<ExpiringBatchDto>?> GetExpiringAsync(int maxRows = 50)
        => GetAsync<List<ExpiringBatchDto>>($"api/inventory/expiring?maxRows={maxRows}");

    // ── inventory control report (spec 004) ────────────────────────────────
    public Task<List<InventoryReportRowDto>?> GetInventoryReportAsync(Guid? branchId)
        => GetAsync<List<InventoryReportRowDto>>("api/inventory/report" + BranchQuery(branchId));

    /// <summary>The report as CSV bytes (with BOM), or null when the server refused it.</summary>
    public async Task<byte[]?> GetInventoryReportCsvAsync(Guid? branchId)
    {
        var message = await BuildAsync(HttpMethod.Get, "api/inventory/report.csv" + BranchQuery(branchId), null);
        var response = await http.SendAsync(message);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
    }

    /// <summary>Saves a product's reorder data; keeps the reason when it fails (409 repeated code, 400 invalid).</summary>
    public async Task<(bool Ok, System.Net.HttpStatusCode? Status, string? Error)> SetInventorySettingsAsync(
        Guid productId,
        InventorySettingsRequest request)
    {
        var message = await BuildAsync(HttpMethod.Put, $"api/inventory/products/{productId}/settings", request);
        var response = await http.SendAsync(message);

        if (response.IsSuccessStatusCode)
        {
            return (true, response.StatusCode, null);
        }

        var body = await response.Content.ReadAsStringAsync();
        return (false, response.StatusCode, ExtractError(body));
    }

    private static string BranchQuery(Guid? branchId) => branchId is { } id ? $"?branchId={id}" : string.Empty;

    // ── users (spec 005, owner only) ────────────────────────────────────────
    public Task<List<UserDto>?> GetUsersAsync() => GetAsync<List<UserDto>>("api/users");

    public Task<(UserDto? Value, System.Net.HttpStatusCode? Status, string? Error)> CreateUserAsync(CreateUserRequest request)
        => SendForResultAsync<UserDto>(HttpMethod.Post, "api/users", request);

    public Task<(UserDto? Value, System.Net.HttpStatusCode? Status, string? Error)> UpdateUserAsync(Guid id, UpdateUserRequest request)
        => SendForResultAsync<UserDto>(HttpMethod.Put, $"api/users/{id}", request);

    public async Task<(bool Ok, string? Error)> ResetUserPasswordAsync(Guid id, string password)
    {
        var message = await BuildAsync(HttpMethod.Post, $"api/users/{id}/password", new ResetPasswordRequest { Password = password });
        var response = await http.SendAsync(message);
        return response.IsSuccessStatusCode
            ? (true, null)
            : (false, ExtractError(await response.Content.ReadAsStringAsync()));
    }

    // ── panel "Mi día" (spec 006) ───────────────────────────────────────────
    // El servidor calcula "hoy" con el desfase del navegador: Ecuador es −300 minutos.
    private static int OffsetMinutes() => (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;

    private static string SellerQuery(string name, Guid? id) => id is { } value ? $"&{name}={value}" : string.Empty;

    public Task<SellerSalesSummaryDto?> GetSellerSummaryAsync(int days, Guid? sellerId = null)
        => GetAsync<SellerSalesSummaryDto>(
            $"api/orders/seller-summary?days={days}&offsetMinutes={OffsetMinutes()}{SellerQuery("sellerId", sellerId)}");

    public Task<PaymentDaySummaryDto?> GetPaymentDaySummaryAsync(Guid? userId = null)
        => GetAsync<PaymentDaySummaryDto>(
            $"api/payments/summary?days=1&offsetMinutes={OffsetMinutes()}{SellerQuery("userId", userId)}");

    public Task<List<PettyCashExpenseDto>?> GetPettyCashAsync(Guid? userId = null)
        => GetAsync<List<PettyCashExpenseDto>>(
            $"api/payments/petty-cash?days=1&offsetMinutes={OffsetMinutes()}{SellerQuery("userId", userId)}");

    public Task<(PettyCashExpenseDto? Value, System.Net.HttpStatusCode? Status, string? Error)> CreatePettyCashAsync(
        CreatePettyCashExpenseRequest request)
        => SendForResultAsync<PettyCashExpenseDto>(HttpMethod.Post, "api/payments/petty-cash", request);

    public async Task<(bool Ok, string? Error)> VoidPettyCashAsync(Guid id)
    {
        var message = await BuildAsync(HttpMethod.Post, $"api/payments/petty-cash/{id}/void", null);
        var response = await http.SendAsync(message);
        return response.IsSuccessStatusCode
            ? (true, null)
            : (false, ExtractError(await response.Content.ReadAsStringAsync()));
    }

    public Task<PettyCashSettingsDto?> GetPettyCashSettingsAsync()
        => GetAsync<PettyCashSettingsDto>("api/payments/petty-cash/settings");

    public Task<(PettyCashSettingsDto? Value, System.Net.HttpStatusCode? Status, string? Error)> SetPettyCashSettingsAsync(decimal maxAmount)
        => SendForResultAsync<PettyCashSettingsDto>(
            HttpMethod.Put, "api/payments/petty-cash/settings", new PettyCashSettingsDto { MaxAmount = maxAmount });

    public Task<InventoryAlertsDto?> GetInventoryAlertsAsync() => GetAsync<InventoryAlertsDto>("api/inventory/alerts");

    /// <summary>Sends a request and keeps the status and reason when it fails (409, 400...).</summary>
    private async Task<(T? Value, System.Net.HttpStatusCode? Status, string? Error)> SendForResultAsync<T>(
        HttpMethod method,
        string path,
        object body)
    {
        var message = await BuildAsync(method, path, body);
        var response = await http.SendAsync(message);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<T>(Json), response.StatusCode, null);
        }

        return (default, response.StatusCode, ExtractError(await response.Content.ReadAsStringAsync()));
    }
}
