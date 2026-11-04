using System.Net;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Auth;

/// <summary>
/// Contract of the Identity service (contracts/IdentityApi.cs), exercised through the gateway.
/// Requires <c>SAGASALES_GATEWAY_URL</c> and a running stack.
/// </summary>
public class AuthContractTests
{
    private static string NewSlug() => "ct-" + Guid.NewGuid().ToString("N")[..20];

    [Fact]
    public async Task Register_returns_201_and_refuses_the_same_slug_with_409()
    {
        if (Gateway.TryAnnounceSkip(nameof(Register_returns_201_and_refuses_the_same_slug_with_409)))
        {
            return;
        }

        using var client = Gateway.CreateClient();

        var slug = NewSlug();
        var request = new RegisterTenantRequest
        {
            Slug = slug,
            DisplayName = "Contract store",
            AdminEmail = $"{slug}@contract.local",
            AdminPassword = "Demo12345!",
        };

        using var created = await client.PostAsJsonAsync("api/auth/register-tenant", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var body = await created.Content.ReadFromJsonAsync<RegisterTenantResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.TenantId);

        using var duplicate = await client.PostAsJsonAsync("api/auth/register-tenant", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Login_returns_a_token_with_the_tenant_claim()
    {
        if (Gateway.TryAnnounceSkip(nameof(Login_returns_a_token_with_the_tenant_claim)))
        {
            return;
        }

        using var client = Gateway.CreateClient();

        var slug = NewSlug();
        var email = $"{slug}@contract.local";
        const string Password = "Demo12345!";

        using var created = await client.PostAsJsonAsync("api/auth/register-tenant", new RegisterTenantRequest
        {
            Slug = slug,
            DisplayName = "Contract store",
            AdminEmail = email,
            AdminPassword = Password,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var login = await client.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password,
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal("admin", body.User!.Role);
        Assert.NotEqual(Guid.Empty, body.User.TenantId);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401_without_revealing_the_account()
    {
        if (Gateway.TryAnnounceSkip(nameof(Login_with_a_wrong_password_returns_401_without_revealing_the_account)))
        {
            return;
        }

        using var client = Gateway.CreateClient();

        var slug = NewSlug();
        var email = $"{slug}@contract.local";

        using var created = await client.PostAsJsonAsync("api/auth/register-tenant", new RegisterTenantRequest
        {
            Slug = slug,
            DisplayName = "Contract store",
            AdminEmail = email,
            AdminPassword = "Demo12345!",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var wrongPassword = await client.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "WrongPassword!",
        });
        using var unknownEmail = await client.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            Email = $"nobody-{slug}@contract.local",
            Password = "Demo12345!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        // Same message for both: no account enumeration.
        var wrongBody = await wrongPassword.Content.ReadAsStringAsync();
        var unknownBody = await unknownEmail.Content.ReadAsStringAsync();
        Assert.Equal(wrongBody, unknownBody);
    }
}
