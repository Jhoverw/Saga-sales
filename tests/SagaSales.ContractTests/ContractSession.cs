using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests;

/// <summary>
/// A throwaway store signed in against the live gateway: one HTTP client, its token and tenant.
/// Optionally creates the default branch, because a store without a branch cannot hold stock.
/// </summary>
internal sealed class ContractSession : IDisposable
{
    private ContractSession(HttpClient client, string token, Guid tenantId)
    {
        Client = client;
        Token = token;
        TenantId = tenantId;
    }

    public HttpClient Client { get; }

    public string Token { get; }

    public Guid TenantId { get; }

    public static async Task<ContractSession> CreateAsync(bool withBranch = false)
    {
        var client = Gateway.CreateClient();
        var slug = "ct-" + Guid.NewGuid().ToString("N")[..16];
        var email = $"{slug}@contract.local";
        const string Password = "Demo12345!";

        using var register = await client.PostAsJsonAsync("api/auth/register-tenant", new RegisterTenantRequest
        {
            Slug = slug,
            DisplayName = "Contract store",
            AdminEmail = email,
            AdminPassword = Password,
        });
        register.EnsureSuccessStatusCode();

        using var login = await client.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password,
        });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.User);

        var session = new ContractSession(client, body.AccessToken, body.User!.TenantId);

        if (withBranch)
        {
            using var branch = await session.SendAsync(
                HttpMethod.Post,
                "api/branches",
                new CreateBranchRequest { Name = "Principal", IsDefault = true });
            Assert.Equal(HttpStatusCode.Created, branch.StatusCode);

            // A store that sells needs a VAT rate in force (V-03), like the real setup in Configuración Fiscal.
            using var tax = await session.SendAsync(
                HttpMethod.Post,
                "api/taxes",
                new CreateTaxRateRequest(0.15m, new DateOnly(2000, 1, 1), null, "IVA contrato"));
            Assert.Equal(HttpStatusCode.OK, tax.StatusCode);
        }

        return session;
    }

    public HttpRequestMessage Request(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", TenantId.ToString());

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
        => Client.SendAsync(Request(method, path, body));

    public void Dispose() => Client.Dispose();
}
