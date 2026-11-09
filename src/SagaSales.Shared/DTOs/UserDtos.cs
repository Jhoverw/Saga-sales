namespace SagaSales.Shared.DTOs;

/// <summary>A staff user of the store (spec 005). Role: admin (dueño), manager, sales (cajero).</summary>
public class UserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class ResetPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}
