namespace SagaSales.Shared.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public class UserInfo
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public Guid TenantId { get; set; }
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public int ExpiresIn { get; set; }

    public UserInfo? User { get; set; }
}

public class RegisterTenantRequest
{
    public string Slug { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? CustomDomain { get; set; }

    public string AdminEmail { get; set; } = string.Empty;

    public string AdminPassword { get; set; } = string.Empty;
}

public class RegisterTenantResponse
{
    public Guid TenantId { get; set; }

    public string AdminEmail { get; set; } = string.Empty;

    public string Role { get; set; } = "admin";
}
