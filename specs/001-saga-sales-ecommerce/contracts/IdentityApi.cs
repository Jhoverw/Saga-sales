// Identity Service — REST contract.
// Declarative contract: this file documents the API, it is not compiled.
// Added in this revision: E-01 had no contract at all, yet it is the first MVP story.

namespace SagaSales.Contracts;

public class RegisterTenantRequest
{
    /// <summary>Lowercase, digits and hyphens. Unique across the platform (FR-001).</summary>
    public string Slug { get; set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>Becomes the store administrator. Unique inside the store.</summary>
    public string AdminEmail { get; set; } = "";

    /// <summary>At least 8 characters. Stored only as a PBKDF2 hash, never in clear text.</summary>
    public string AdminPassword { get; set; } = "";

    public string? CustomDomain { get; set; }
}

public class RegisterTenantResponse
{
    public Guid TenantId { get; set; }
    public string AdminEmail { get; set; } = "";
    public string Role { get; set; } = "admin";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public UserInfo? User { get; set; }
}

public class UserInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Guid TenantId { get; set; }
}

// HTTP Endpoints — anonymous by design; this is where a token is obtained.
//
// POST /api/auth/register-tenant → 201 RegisterTenantResponse
//      409 CONFLICT if the slug is taken · 400 VALIDATION_ERROR if the password is too short,
//      the slug is malformed or the email is invalid.
// POST /api/auth/login           → 200 LoginResponse · 401 UNAUTHORIZED
//
// Token claims (FR-002, FR-004)
//   sub        → user id
//   email      → user email
//   role       → admin | manager | sales | customer
//   tenant_id  → store id; every downstream service resolves the store from it
//
// Rules
//  - A wrong email and a wrong password must produce the same generic 401: no account enumeration.
//  - A request with no resolvable tenant is rejected before touching business data (FR-004).
//  - The bearer token is validated at the gateway and again by each service.
//
// No domain events are published by this service.
