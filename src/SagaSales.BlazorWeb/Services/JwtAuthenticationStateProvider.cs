using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace SagaSales.BlazorWeb.Services;

/// <summary>
/// Derives the authentication state from the JWT stored in localStorage.
/// The previous implementation used MSAL/IdentityServer, which this solution never configured.
/// </summary>
public sealed class JwtAuthenticationStateProvider(TokenStore store) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await store.GetTokenAsync();
        return new AuthenticationState(BuildPrincipal(token));
    }

    public void NotifyChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public static ClaimsPrincipal BuildPrincipal(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Anonymous;
        }

        var claims = ParseClaims(token);
        var expires = claims.FirstOrDefault(c => c.Type == "exp")?.Value;

        if (expires is not null &&
            long.TryParse(expires, out var seconds) &&
            DateTimeOffset.FromUnixTimeSeconds(seconds) <= DateTimeOffset.UtcNow)
        {
            return Anonymous;
        }

        return claims.Count == 0
            ? Anonymous
            : new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
    }

    public static List<Claim> ParseClaims(string token)
    {
        var claims = new List<Claim>();
        var parts = token.Split('.');

        if (parts.Length < 2)
        {
            return claims;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(Pad(parts[1])));
            using var document = JsonDocument.Parse(payload);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var type = property.Name switch
                {
                    "sub" => ClaimTypes.NameIdentifier,
                    "email" => ClaimTypes.Email,
                    "role" => ClaimTypes.Role,
                    "name" => ClaimTypes.Name,
                    "tenant_id" => SagaSales.Shared.Constants.SagaSalesConstants.Claims.TenantId,
                    _ => property.Name,
                };

                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        claims.Add(new Claim(type, item.ToString()));
                    }
                }
                else
                {
                    claims.Add(new Claim(type, property.Value.ToString()));
                }
            }
        }
        catch (FormatException)
        {
            // Malformed token: treat the user as anonymous.
        }
        catch (JsonException)
        {
            // Malformed payload: treat the user as anonymous.
        }

        return claims;
    }

    private static string Pad(string input)
    {
        var value = input.Replace('-', '+').Replace('_', '/');

        return (value.Length % 4) switch
        {
            2 => value + "==",
            3 => value + "=",
            _ => value,
        };
    }
}
