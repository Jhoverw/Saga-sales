using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Identity;

/// <summary>
/// Spec 005 against db/identity/V004: the owner manages users. Requires <c>SAGASALES_TEST_SQL</c>
/// on <c>saga_identity_test</c>.
/// </summary>
public class UserManagementTests
{
    private const string Hash = "100000.c2FsdA==.aGFzaA==";

    [Fact]
    public async Task A_created_user_is_active_with_its_role_and_can_be_found_to_sign_in()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_created_user_is_active_with_its_role_and_can_be_found_to_sign_in)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var store = await CreateStoreAsync(connection);
        var email = NewEmail("cajera");

        var user = await CreateUserAsync(connection, store.Id, email, "sales", "María");
        var signIn = await FindByEmailAsync(connection, email);

        Assert.True(user.IsActive);
        Assert.Equal("sales", user.Role);
        Assert.Equal("María", user.DisplayName);
        Assert.NotNull(signIn);
        Assert.Equal(store.Id, signIn!.TenantId);
    }

    [Fact]
    public async Task An_email_used_by_an_active_user_of_another_store_is_refused()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(An_email_used_by_an_active_user_of_another_store_is_refused)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var first = await CreateStoreAsync(connection);
        var second = await CreateStoreAsync(connection);
        var email = NewEmail("compartido");
        await CreateUserAsync(connection, first.Id, email, "sales", "Uno");

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            CreateUserAsync(connection, second.Id, email, "sales", "Dos"));

        Assert.Contains("CONFLICT", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_store_cannot_be_registered_with_an_email_already_signing_in_elsewhere()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_store_cannot_be_registered_with_an_email_already_signing_in_elsewhere)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var store = await CreateStoreAsync(connection);
        var email = NewEmail("dueno");
        await CreateUserAsync(connection, store.Id, email, "manager", "Admin");

        var exception = await Assert.ThrowsAsync<SqlException>(() => CreateStoreAsync(connection, email));

        Assert.Contains("CONFLICT", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_list_only_shows_the_store_users()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_list_only_shows_the_store_users)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var mine = await CreateStoreAsync(connection);
        var other = await CreateStoreAsync(connection);
        await CreateUserAsync(connection, mine.Id, NewEmail("a"), "sales", "A");
        await CreateUserAsync(connection, other.Id, NewEmail("b"), "sales", "B");

        var users = await ListAsync(connection, mine.Id);

        Assert.Equal(2, users.Count);                       // the owner created with the store + A
        Assert.DoesNotContain(users, u => u.DisplayName == "B");
    }

    [Fact]
    public async Task The_last_active_owner_cannot_be_deactivated_or_demoted()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_last_active_owner_cannot_be_deactivated_or_demoted)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var store = await CreateStoreAsync(connection);

        var deactivate = await Assert.ThrowsAsync<SqlException>(() =>
            UpdateAsync(connection, store.Id, store.AdminUserId, "Dueño", "admin", isActive: false));
        var demote = await Assert.ThrowsAsync<SqlException>(() =>
            UpdateAsync(connection, store.Id, store.AdminUserId, "Dueño", "sales", isActive: true));

        Assert.Contains("CONFLICT", deactivate.Message, StringComparison.Ordinal);
        Assert.Contains("CONFLICT", demote.Message, StringComparison.Ordinal);

        // With a second owner, the first one can step down.
        await CreateUserAsync(connection, store.Id, NewEmail("socio"), "admin", "Socio");
        var updated = await UpdateAsync(connection, store.Id, store.AdminUserId, "Ex dueño", "manager", isActive: true);
        Assert.Equal("manager", updated.Role);
    }

    [Fact]
    public async Task A_deactivated_user_cannot_sign_in_and_reactivating_checks_the_email_again()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_deactivated_user_cannot_sign_in_and_reactivating_checks_the_email_again)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var first = await CreateStoreAsync(connection);
        var second = await CreateStoreAsync(connection);
        var email = NewEmail("rotativo");
        var user = await CreateUserAsync(connection, first.Id, email, "sales", "Rotativo");

        await UpdateAsync(connection, first.Id, user.Id, "Rotativo", "sales", isActive: false);
        Assert.Null(await FindByEmailAsync(connection, email));

        // The email is free while the first user is inactive; reactivating it would collide.
        await CreateUserAsync(connection, second.Id, email, "sales", "Nuevo");
        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            UpdateAsync(connection, first.Id, user.Id, "Rotativo", "sales", isActive: true));
        Assert.Contains("CONFLICT", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_owner_can_reset_a_password()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_owner_can_reset_a_password)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var store = await CreateStoreAsync(connection);
        var user = await CreateUserAsync(connection, store.Id, NewEmail("clave"), "sales", "Clave");

        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", store.Id);
        arguments.Add("user_id", user.Id);
        arguments.Add("password_hash", "100000.bnVldm8=.bnVldm8=");
        await connection.ExecuteAsync("dbo.sp_user_set_password", arguments, commandType: CommandType.StoredProcedure);

        var stored = await connection.QuerySingleAsync<string>(
            "SELECT password_hash FROM dbo.users WHERE id = @Id", new { user.Id });
        Assert.Equal("100000.bnVldm8=.bnVldm8=", stored);
    }

    [Fact]
    public async Task An_unknown_role_is_refused()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(An_unknown_role_is_refused)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var store = await CreateStoreAsync(connection);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            CreateUserAsync(connection, store.Id, NewEmail("rol"), "superuser", "X"));

        Assert.Contains("VALIDATION_ERROR", exception.Message, StringComparison.Ordinal);
    }

    private static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@integration.local";

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("identity"));
        await connection.OpenAsync();
        return connection;
    }

    private static Task<StoreRow> CreateStoreAsync(SqlConnection connection, string? adminEmail = null)
    {
        var slug = "test-" + Guid.NewGuid().ToString("N");
        var arguments = new DynamicParameters();
        arguments.Add("slug", slug);
        arguments.Add("display_name", "Tienda de prueba");
        arguments.Add("admin_email", adminEmail ?? $"{slug}@integration.local");
        arguments.Add("admin_password_hash", Hash);
        arguments.Add("custom_domain", null);

        return connection.QuerySingleAsync<StoreRow>("dbo.sp_tenant_create", arguments, commandType: CommandType.StoredProcedure);
    }

    private static Task<UserRow> CreateUserAsync(SqlConnection connection, Guid tenantId, string email, string role, string name)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("email", email);
        arguments.Add("password_hash", Hash);
        arguments.Add("role", role);
        arguments.Add("display_name", name);

        return connection.QuerySingleAsync<UserRow>("dbo.sp_user_create", arguments, commandType: CommandType.StoredProcedure);
    }

    private static Task<UserRow> UpdateAsync(SqlConnection connection, Guid tenantId, Guid userId, string name, string role, bool isActive)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("user_id", userId);
        arguments.Add("display_name", name);
        arguments.Add("role", role);
        arguments.Add("is_active", isActive);

        return connection.QuerySingleAsync<UserRow>("dbo.sp_user_update", arguments, commandType: CommandType.StoredProcedure);
    }

    private static async Task<List<UserRow>> ListAsync(SqlConnection connection, Guid tenantId)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        var rows = await connection.QueryAsync<UserRow>("dbo.sp_user_list", arguments, commandType: CommandType.StoredProcedure);
        return rows.AsList();
    }

    private static async Task<SignInRow?> FindByEmailAsync(SqlConnection connection, string email)
    {
        var arguments = new DynamicParameters();
        arguments.Add("email", email);
        return await connection.QuerySingleOrDefaultAsync<SignInRow>(
            "dbo.sp_user_find_by_email", arguments, commandType: CommandType.StoredProcedure);
    }

    private sealed class StoreRow
    {
        public Guid Id { get; set; }

        public Guid AdminUserId { get; set; }
    }

    private sealed class UserRow
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }

    private sealed class SignInRow
    {
        public Guid TenantId { get; set; }
    }
}
