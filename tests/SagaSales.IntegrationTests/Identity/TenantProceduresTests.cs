using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Identity;

/// <summary>
/// Exercises the procedures of <c>db/identity/V002__functions.sql</c> against a real SQL Server.
/// Run <c>scripts/apply-db.ps1 -Service identity</c> first; the suite skips when
/// <c>SAGASALES_TEST_SQL</c> is not set.
/// </summary>
public class TenantProceduresTests
{
    private static string NewSlug() => "test-" + Guid.NewGuid().ToString("N");

    private static string NewEmail(string slug) => $"{slug}@integration.local";

    [Fact]
    public async Task Tenant_create_writes_the_store_and_its_administrator()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Tenant_create_writes_the_store_and_its_administrator)))
        {
            return;
        }

        var slug = NewSlug();
        var email = NewEmail(slug);

        await using var connection = new SqlConnection(TestDatabase.ForService("identity"));
        await connection.OpenAsync();

        var arguments = new DynamicParameters();
        arguments.Add("slug", slug);
        arguments.Add("display_name", "Integration store");
        arguments.Add("admin_email", email);
        arguments.Add("admin_password_hash", "100000.c2FsdA==.aGFzaA==");
        arguments.Add("custom_domain", null);

        var registration = await connection.QuerySingleAsync<TenantRegistrationRow>(
            "dbo.sp_tenant_create",
            arguments,
            commandType: CommandType.StoredProcedure);

        Assert.NotEqual(Guid.Empty, registration.Id);
        Assert.NotEqual(Guid.Empty, registration.AdminUserId);
        Assert.Equal(slug, registration.Slug);
        Assert.Equal(email, registration.AdminEmail);

        // The administrator really exists and is linked to that store.
        var stored = await connection.QuerySingleAsync<AdminRow>(
            "SELECT id AS Id, tenant_id AS TenantId, role AS Role FROM dbo.users WHERE email = @Email",
            new { Email = email });

        Assert.Equal(registration.AdminUserId, stored.Id);
        Assert.Equal(registration.Id, stored.TenantId);
        Assert.Equal("admin", stored.Role);
    }

    [Fact]
    public async Task Tenant_create_refuses_a_duplicate_slug_and_leaves_no_orphan_user()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Tenant_create_refuses_a_duplicate_slug_and_leaves_no_orphan_user)))
        {
            return;
        }

        var slug = NewSlug();
        var firstEmail = NewEmail(slug);
        var secondEmail = $"other-{slug}@integration.local";

        await using var connection = new SqlConnection(TestDatabase.ForService("identity"));
        await connection.OpenAsync();

        DynamicParameters Args(string email) => new DynamicParameters()
            .WithSlug(slug)
            .WithEmail(email);

        await connection.QuerySingleAsync<TenantRegistrationRow>(
            "dbo.sp_tenant_create", Args(firstEmail), commandType: CommandType.StoredProcedure);

        var duplicate = await Assert.ThrowsAsync<SqlException>(() =>
            connection.QuerySingleAsync<TenantRegistrationRow>(
                "dbo.sp_tenant_create", Args(secondEmail), commandType: CommandType.StoredProcedure));

        Assert.Contains("CONFLICT", duplicate.Message, StringComparison.OrdinalIgnoreCase);

        // The rollback must have removed the second administrator as well.
        var users = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.users WHERE email = @Email", new { Email = secondEmail });

        Assert.Equal(0, users);
    }

    [Fact]
    public async Task Find_by_email_reports_ambiguity_instead_of_guessing()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Find_by_email_reports_ambiguity_instead_of_guessing)))
        {
            return;
        }

        // The same address in two different stores must not authenticate silently.
        var shared = $"shared-{Guid.NewGuid():N}@integration.local";

        await using var connection = new SqlConnection(TestDatabase.ForService("identity"));
        await connection.OpenAsync();

        foreach (var slug in new[] { NewSlug(), NewSlug() })
        {
            await connection.QuerySingleAsync<TenantRegistrationRow>(
                "dbo.sp_tenant_create",
                new DynamicParameters()
                    .WithSlug(slug)
                    .WithEmail(shared),
                commandType: CommandType.StoredProcedure);
        }

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            connection.QueryAsync<object>(
                "dbo.sp_user_find_by_email",
                new { email = shared },
                commandType: CommandType.StoredProcedure));

        Assert.Contains("CONFLICT", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TenantRegistrationRow
    {
        public Guid Id { get; set; }

        public string Slug { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public short Status { get; set; }

        public Guid AdminUserId { get; set; }

        public string AdminEmail { get; set; } = string.Empty;
    }

    private sealed class AdminRow
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public string Role { get; set; } = string.Empty;
    }
}

internal static class ProcedureArgumentExtensions
{
    public static DynamicParameters WithSlug(this DynamicParameters parameters, string slug)
    {
        parameters.Add("slug", slug);
        parameters.Add("display_name", "Integration store");
        parameters.Add("admin_password_hash", "100000.c2FsdA==.aGFzaA==");
        parameters.Add("custom_domain", null);
        return parameters;
    }

    public static DynamicParameters WithEmail(this DynamicParameters parameters, string email)
    {
        parameters.Add("admin_email", email);
        return parameters;
    }
}
