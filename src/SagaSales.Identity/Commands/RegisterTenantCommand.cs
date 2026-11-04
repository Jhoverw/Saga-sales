namespace SagaSales.Identity.Commands;

/// <summary>
/// Registers tenant plus its first admin user. The admin password is now supplied by the
/// caller and hashed with PBKDF2 — previously it was generated, weakly hashed and printed
/// to the log.
/// </summary>
public record RegisterTenantCommand(
    string Slug,
    string DisplayName,
    string AdminEmail,
    string AdminPassword,
    string? CustomDomain = null) : ICommand<RegisterTenantResponse>;

public sealed class RegisterTenantCommandHandler(
    TenantDbContext dbContext,
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

        var slugTaken = await dbContext.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken);
        if (slugTaken)
        {
            throw new ConflictException($"Tenant slug '{slug}' already exists.");
        }

        var tenant = new Domain.Tenant
        {
            Slug = slug,
            DisplayName = request.DisplayName.Trim(),
            Plan = "trial",
            Status = TenantStatus.Trial,
            CustomDomain = request.CustomDomain,
        };

        var admin = new Domain.User
        {
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = passwordHasher.Hash(request.AdminPassword),
            Role = "admin",
            DisplayName = "Administrator",
            IsActive = true,
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<RegisterTenantResponse>.Success(new RegisterTenantResponse
        {
            TenantId = tenant.Id,
            AdminEmail = admin.Email,
            Role = admin.Role,
        });
    }
}
