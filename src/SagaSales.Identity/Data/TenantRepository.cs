namespace SagaSales.Identity.Data;

using Dapper;
using SagaSales.BuildingBlocks.Data;

/// <summary>
/// Identity persistence, through the procedures in <c>db/identity/V002__functions.sql</c>.
/// There is no ORM: this repository is the only way in.
/// </summary>
public interface ITenantRepository
{
    /// <summary>Creates the store and its first administrator in one transaction.</summary>
    Task<TenantRegistration> CreateAsync(
        string slug,
        string displayName,
        string adminEmail,
        string passwordHash,
        string? customDomain,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds the user for sign-in. Returns <c>null</c> when nobody matches and throws
    /// <c>CONFLICT</c> when the email exists in more than one store.
    /// </summary>
    Task<AuthenticatedUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken);

    Task TouchLoginAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}

public sealed record TenantRegistration(
    Guid Id,
    string Slug,
    string DisplayName,
    short Status,
    Guid AdminUserId,
    string AdminEmail);

public sealed record AuthenticatedUser(
    Guid Id,
    Guid TenantId,
    string Email,
    string PasswordHash,
    string Role,
    string DisplayName);

public sealed class TenantRepository(IStoredProcedureExecutor executor, ILogger<TenantRepository> logger)
    : ITenantRepository
{
    public async Task<TenantRegistration> CreateAsync(
        string slug,
        string displayName,
        string adminEmail,
        string passwordHash,
        string? customDomain,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("slug", slug)
            .With("display_name", displayName)
            .With("admin_email", adminEmail)
            .With("admin_password_hash", passwordHash)
            .With("custom_domain", customDomain);

        // The store does not exist yet, so this call cannot be tenant-scoped.
        var registration = await executor.QueryProcSingleAsync<TenantRegistration>(
            "dbo.sp_tenant_create",
            arguments,
            tenantScoped: false,
            cancellationToken);

        if (registration is null)
        {
            throw new SagaSales.Shared.Exceptions.SagaSalesException(
                "TenantCreateFailed",
                "The store was created but no confirmation was returned.",
                500);
        }

        logger.LogInformation("Store {Slug} created with administrator {Email}", slug, adminEmail);
        return registration;
    }

    public async Task<AuthenticatedUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("email", email);

        // Sign-in runs before a token exists: tenantScoped must be false here.
        return await executor.QueryProcSingleAsync<AuthenticatedUser>(
            "dbo.sp_user_find_by_email",
            arguments,
            tenantScoped: false,
            cancellationToken);
    }

    public async Task TouchLoginAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("p_tenant_id", tenantId)
            .With("user_id", userId);

        await executor.ExecuteProcAsync(
            "dbo.sp_user_touch_login",
            arguments,
            tenantScoped: false,
            cancellationToken);
    }
}
