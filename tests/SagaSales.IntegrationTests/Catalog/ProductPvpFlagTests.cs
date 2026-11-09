using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Catalog;

/// <summary>
/// Spec 003 (FR-003, FR-004) against db/catalog/V022 and V023: new products are priced with VAT
/// included, reads expose both flags, and editing the price marks it as PVP.
/// Requires <c>SAGASALES_TEST_SQL</c> on <c>saga_catalog_test</c>.
/// </summary>
public class ProductPvpFlagTests
{
    [Fact]
    public async Task A_new_product_is_priced_with_vat_included()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_new_product_is_priced_with_vat_included)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var productId = await CreateAsync(connection, tenantId, isTaxable: false);

        var product = await GetAsync(connection, tenantId, productId);

        Assert.True(product.PriceIncludesTax);
        Assert.False(product.IsTaxable);
    }

    [Fact]
    public async Task The_list_returns_both_flags()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_list_returns_both_flags)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await CreateAsync(connection, tenantId, isTaxable: false);

        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        var rows = (await connection.QueryAsync<FlagRow>(
            "dbo.sp_product_list", arguments, commandType: CommandType.StoredProcedure)).AsList();

        Assert.Single(rows);
        Assert.False(rows[0].IsTaxable);
        Assert.True(rows[0].PriceIncludesTax);
    }

    [Fact]
    public async Task Editing_the_price_of_an_unconverted_product_marks_it_as_pvp()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Editing_the_price_of_an_unconverted_product_marks_it_as_pvp)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var productId = await CreateAsync(connection, tenantId, isTaxable: true);

        // A product priced before spec 003.
        await connection.ExecuteAsync(
            "UPDATE dbo.products SET price_includes_tax = 0 WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = productId, TenantId = tenantId });

        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("product_id", productId);
        arguments.Add("name", "Coca-Cola 500 ml");
        arguments.Add("base_price", 0.40m);
        arguments.Add("stock_quantity", 0);
        await connection.ExecuteAsync("dbo.sp_product_update", arguments, commandType: CommandType.StoredProcedure);

        var product = await GetAsync(connection, tenantId, productId);

        Assert.True(product.PriceIncludesTax);
        Assert.Equal(0.40m, product.Price);
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<Guid> CreateAsync(SqlConnection connection, Guid tenantId, bool isTaxable)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("name", "Coca-Cola 500 ml");
        arguments.Add("base_price", 0.40m);
        arguments.Add("created_by", Guid.NewGuid());
        arguments.Add("is_taxable", isTaxable);

        var row = await connection.QuerySingleAsync<FlagRow>(
            "dbo.sp_product_create", arguments, commandType: CommandType.StoredProcedure);
        return row.Id;
    }

    private static Task<FlagRow> GetAsync(SqlConnection connection, Guid tenantId, Guid productId)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("product_id", productId);

        return connection.QuerySingleAsync<FlagRow>(
            "dbo.sp_product_get", arguments, commandType: CommandType.StoredProcedure);
    }

    private sealed class FlagRow
    {
        public Guid Id { get; set; }

        public decimal Price { get; set; }

        public bool IsTaxable { get; set; }

        public bool PriceIncludesTax { get; set; }
    }
}
