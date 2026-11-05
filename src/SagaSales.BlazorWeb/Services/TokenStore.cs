using Microsoft.JSInterop;

namespace SagaSales.BlazorWeb.Services;

/// <summary>
/// Stores the JWT in browser localStorage through the <c>window.sagaStorage</c> facade declared in
/// index.html.
/// <para>
/// Reading goes through <c>sagaStorage.get</c> instead of <c>localStorage.getItem</c>: the latter
/// marshals a host object, and when it fails the Blazor error path itself throws
/// ("Cannot read properties of undefined (reading 'getI32')"), taking the whole renderer down.
/// A storage failure must never stop the app from rendering: it degrades to "anonymous" and the
/// user simply sees the sign-in page.
/// </para>
/// </summary>
public sealed class TokenStore(IJSRuntime js)
{
    private const string TokenKey = "saga-sales.token";
    private const string TenantKey = "saga-sales.tenant";

    public async Task<string?> GetTokenAsync() => await ReadAsync(TokenKey);

    public async Task<string?> GetTenantIdAsync() => await ReadAsync(TenantKey);

    public async Task SetTokenAsync(string token) => await WriteAsync(TokenKey, token);

    public async Task SetTenantIdAsync(string tenantId) => await WriteAsync(TenantKey, tenantId);

    public async Task ClearAsync()
    {
        await RemoveAsync(TokenKey);
        await RemoveAsync(TenantKey);
    }

    private async Task<string?> ReadAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("sagaStorage.get", key);
        }
        catch (Exception)
        {
            // Storage blocked or interop unavailable: treat as no session.
            return null;
        }
    }

    private async Task WriteAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("sagaStorage.set", key, value);
        }
        catch (Exception)
        {
            // The session will not survive a reload, but the current screen still works.
        }
    }

    private async Task RemoveAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("sagaStorage.remove", key);
        }
        catch (Exception)
        {
            // Nothing to do: the entry could not be read either.
        }
    }
}
