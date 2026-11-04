using Microsoft.JSInterop;

namespace SagaSales.BlazorWeb.Services;

/// <summary>
/// Stores the JWT in browser localStorage through JS interop.
/// (Replaces the previous <c>LocalStorageService</c>, which was referenced but never existed.)
/// </summary>
public sealed class TokenStore(IJSRuntime js)
{
    private const string TokenKey = "saga-sales.token";
    private const string TenantKey = "saga-sales.tenant";

    public async Task<string?> GetTokenAsync()
        => await js.InvokeAsync<string?>("localStorage.getItem", TokenKey);

    public async Task SetTokenAsync(string token)
        => await js.InvokeVoidAsync("localStorage.setItem", TokenKey, token);

    public async Task<string?> GetTenantIdAsync()
        => await js.InvokeAsync<string?>("localStorage.getItem", TenantKey);

    public async Task SetTenantIdAsync(string tenantId)
        => await js.InvokeVoidAsync("localStorage.setItem", TenantKey, tenantId);

    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", TenantKey);
    }
}
