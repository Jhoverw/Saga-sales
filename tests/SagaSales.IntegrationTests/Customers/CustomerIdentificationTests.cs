using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Customers;

/// <summary>
/// Spec 002 (FR-002, FR-006, FR-007, FR-008): identification, business name and optional email
/// in <c>db/customer/V003</c> and <c>V004</c>. Requires <c>SAGASALES_TEST_SQL</c> and the customer
/// schema applied to <c>saga_customer_test</c>. Identification numbers are synthetic.
/// </summary>
public class CustomerIdentificationTests
{
    private const short Ruc = 4;
    private const short Cedula = 5;

    [Fact]
    public async Task Create_with_identification_and_no_email_returns_the_new_fields()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Create_with_identification_and_no_email_returns_the_new_fields)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();

        var created = await CreateAsync(connection, tenantId, Cedula, "1700000001", "Ana", "Pérez");

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Null(created.Email);
        Assert.Equal(Cedula, created.IdentificationType);
        Assert.Equal("1700000001", created.IdentificationNumber);
        Assert.Null(created.BusinessName);
    }

    [Fact]
    public async Task Create_refuses_a_duplicate_identification_in_the_same_store()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Create_refuses_a_duplicate_identification_in_the_same_store)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await CreateAsync(connection, tenantId, Cedula, "0912345675", "Luis", "Mora");

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            CreateAsync(connection, tenantId, Cedula, "0912345675", "Otro", "Nombre"));

        Assert.Contains("CONFLICT", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_identification_is_allowed_in_another_store()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_same_identification_is_allowed_in_another_store)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        var first = await CreateAsync(connection, Guid.NewGuid(), Cedula, "2400000010", "Eva", "Ruiz");
        var second = await CreateAsync(connection, Guid.NewGuid(), Cedula, "2400000010", "Eva", "Ruiz");

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Two_customers_without_email_do_not_conflict()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Two_customers_without_email_do_not_conflict)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();

        var first = await CreateAsync(connection, tenantId, Cedula, "0102030400", "Uno", "Sin Email");
        var second = await CreateAsync(connection, tenantId, Cedula, "3000000004", "Dos", "Sin Email");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(first.Email);
        Assert.Null(second.Email);
    }

    [Fact]
    public async Task A_company_is_stored_with_its_business_name_and_no_person_names()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_company_is_stored_with_its_business_name_and_no_person_names)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();

        var created = await CreateAsync(
            connection, tenantId, Ruc, "1790012345001", string.Empty, string.Empty, "Comercial Andina S.A.");

        Assert.Equal("Comercial Andina S.A.", created.BusinessName);
        Assert.Equal(Ruc, created.IdentificationType);
    }

    [Fact]
    public async Task A_person_without_names_or_business_name_is_refused()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_person_without_names_or_business_name_is_refused)))
        {
            return;
        }

        await using var connection = await OpenAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            CreateAsync(connection, Guid.NewGuid(), Cedula, "1700000001", string.Empty, string.Empty));

        Assert.Contains("VALIDATION_ERROR", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_finds_customers_by_identification_and_by_business_name()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(List_finds_customers_by_identification_and_by_business_name)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var person = await CreateAsync(connection, tenantId, Cedula, "1700000001", "Ana", "Pérez");
        var company = await CreateAsync(
            connection, tenantId, Ruc, "1790012345001", string.Empty, string.Empty, "Comercial Andina S.A.");

        var byIdentification = await ListAsync(connection, tenantId, "170000");
        var byBusinessName = await ListAsync(connection, tenantId, "Andina");
        var otherStore = await ListAsync(connection, Guid.NewGuid(), "170000");

        Assert.Contains(byIdentification, r => r.Id == person.Id);
        Assert.Single(byBusinessName);
        Assert.Equal(company.Id, byBusinessName[0].Id);
        Assert.Empty(otherStore);
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("customer"));
        await connection.OpenAsync();
        return connection;
    }

    private static Task<CustomerRow> CreateAsync(
        SqlConnection connection,
        Guid tenantId,
        short identificationType,
        string identificationNumber,
        string firstName,
        string lastName,
        string? businessName = null,
        string? email = null)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("first_name", firstName);
        arguments.Add("last_name", lastName);
        arguments.Add("email", email);
        arguments.Add("identification_type", identificationType);
        arguments.Add("identification_number", identificationNumber);
        arguments.Add("business_name", businessName);

        return connection.QuerySingleAsync<CustomerRow>(
            "dbo.sp_customer_create", arguments, commandType: CommandType.StoredProcedure);
    }

    private static async Task<List<CustomerRow>> ListAsync(SqlConnection connection, Guid tenantId, string search)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("page_number", 1);
        arguments.Add("page_size", 50);
        arguments.Add("search", search);

        var rows = await connection.QueryAsync<CustomerRow>(
            "dbo.sp_customer_list", arguments, commandType: CommandType.StoredProcedure);
        return rows.AsList();
    }

    private sealed class CustomerRow
    {
        public Guid Id { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? Email { get; set; }

        public short? IdentificationType { get; set; }

        public string? IdentificationNumber { get; set; }

        public string? BusinessName { get; set; }
    }
}
