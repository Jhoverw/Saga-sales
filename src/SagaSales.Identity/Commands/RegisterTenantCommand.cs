namespace SagaSales.Identity.Commands;

/// <summary>
/// Registers a store plus its first administrator. Uniqueness and the atomic creation of both
/// records are enforced by <c>sp_tenant_create</c>, not by the handler.
/// </summary>
public record RegisterTenantCommand(
    string Slug,
    string DisplayName,
    string AdminEmail,
    string AdminPassword,
    string? CustomDomain = null) : ICommand<RegisterTenantResponse>;

public sealed class RegisterTenantCommandHandler(
    ITenantRepository repository,
    IPasswordHasher passwordHasher) : ICommandHandler<RegisterTenantCommand, RegisterTenantResponse>
{
    public async Task<Result<RegisterTenantResponse>> Handle(
        RegisterTenantCommand request,
        CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Slug, nameof(request.Slug));
        Guard.AgainstNullOrEmpty(request.DisplayName, nameof(request.DisplayName));
        Guard.AgainstNullOrEmpty(request.AdminEmail, nameof(request.AdminEmail));
        Guard.AgainstNullOrEmpty(request.AdminPassword, nameof(request.AdminPassword));

        var slug = request.Slug.Trim().ToLowerInvariant();
        var email = request.AdminEmail.Trim().ToLowerInvariant();

        // A duplicate slug surfaces from the procedure as CONFLICT → 409.
        var registration = await repository.CreateAsync(
            slug,
            request.DisplayName.Trim(),
            email,
            passwordHasher.Hash(request.AdminPassword),
            request.CustomDomain,
            cancellationToken);

        return Result<RegisterTenantResponse>.Success(new RegisterTenantResponse
        {
            TenantId = registration.Id,
            AdminEmail = registration.AdminEmail,
            Role = "admin",
        });
    }
}
