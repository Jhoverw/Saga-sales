namespace SagaSales.Identity.Data;

using SagaSales.BuildingBlocks.Data;

/// <summary>The store's staff users, through db/identity/V004 (spec 005). Always tenant-scoped.</summary>
public interface IUserRepository
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken);

    Task<UserDto> CreateAsync(string email, string passwordHash, string role, string displayName, CancellationToken cancellationToken);

    Task<UserDto> UpdateAsync(Guid userId, string displayName, string role, bool isActive, CancellationToken cancellationToken);

    Task SetPasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken);
}

public sealed class UserRepository(IStoredProcedureExecutor executor) : IUserRepository
{
    public Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken)
        => executor.QueryProcAsync<UserDto>("dbo.sp_user_list", SqlArgs.New(), cancellationToken: cancellationToken);

    public async Task<UserDto> CreateAsync(
        string email,
        string passwordHash,
        string role,
        string displayName,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("email", email)
            .With("password_hash", passwordHash)
            .With("role", role)
            .With("display_name", displayName);

        // A repeated email surfaces from the procedure as CONFLICT → 409.
        var user = await executor.QueryProcSingleAsync<UserDto>("dbo.sp_user_create", arguments, cancellationToken: cancellationToken);
        return user!;
    }

    public async Task<UserDto> UpdateAsync(
        Guid userId,
        string displayName,
        string role,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("user_id", userId)
            .With("display_name", displayName)
            .With("role", role)
            .With("is_active", isActive);

        // Removing the last active owner surfaces as CONFLICT → 409.
        var user = await executor.QueryProcSingleAsync<UserDto>("dbo.sp_user_update", arguments, cancellationToken: cancellationToken);
        return user!;
    }

    public Task SetPasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_user_set_password",
            SqlArgs.New().With("user_id", userId).With("password_hash", passwordHash),
            cancellationToken: cancellationToken);
}
