using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Catalog;

/// <summary>
/// Spec 010 against db/catalog/V027: barcodes per product (unit and box), one search by barcode, internal code or
/// name, and purchase search without services. Requires <c>SAGASALES_TEST_SQL</c> on <c>saga_catalog_test</c>.
/// </summary>
public class ProductBarcodeSearchTests
{
    [Fact]
    public async Task The_unit_and_box_barcodes_find_the_product_with_their_units()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_unit_and_box_barcodes_find_the_product_with_their_units)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var productId = await CreateProductAsync(connection, tenantId, "Galletas Amor");
        await AddBarcodeAsync(connection, tenantId, productId, "7861001234567", 1);
        await AddBarcodeAsync(connection, tenantId, productId, "17861001234564", 12);

        var unit = (await SearchAsync(connection, tenantId, "7861001234567")).First();
        Assert.Equal(productId, unit.Id);
        Assert.True(unit.IsExactMatch);
        Assert.Equal(1, unit.MatchedUnits);

        var box = (await SearchAsync(connection, tenantId, "17861001234564")).First();
        Assert.Equal(productId, box.Id);
        Assert.Equal(12, box.MatchedUnits);
        Assert.Equal("17861001234564", box.Barcode);
        Assert.Equal(12, box.UnitsPerPackage);          // the box code taught the product its box size
    }

    [Fact]
    public async Task Internal_code_and_name_words_without_accents_also_find_it()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Internal_code_and_name_words_without_accents_also_find_it)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var coffee = await CreateProductAsync(connection, tenantId, "Café Molido Premium");
        await CreateProductAsync(connection, tenantId, "Azúcar Blanca");
        await connection.ExecuteAsync(
            "UPDATE dbo.products SET code = N'CM01' WHERE id = @Id", new { Id = coffee });

        var byCode = (await SearchAsync(connection, tenantId, "CM01")).Single();
        Assert.True(byCode.IsExactMatch);
        Assert.Equal("CM01", byCode.Code);

        var byWords = (await SearchAsync(connection, tenantId, "premium cafe")).Single();
        Assert.Equal(coffee, byWords.Id);
        Assert.False(byWords.IsExactMatch);
    }

    [Fact]
    public async Task A_barcode_belongs_to_one_product_per_store()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_barcode_belongs_to_one_product_per_store)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var first = await CreateProductAsync(connection, tenantId, "Leche Entera");
        var second = await CreateProductAsync(connection, tenantId, "Leche Descremada");
        await AddBarcodeAsync(connection, tenantId, first, "7861111111111", 1);

        var taken = await Assert.ThrowsAsync<SqlException>(() => AddBarcodeAsync(connection, tenantId, second, "7861111111111", 1));
        Assert.Contains("CONFLICT:", taken.Message);
        Assert.Contains("Leche Entera", taken.Message);

        // Another store can use the same code.
        var otherTenant = Guid.NewGuid();
        var other = await CreateProductAsync(connection, otherTenant, "Leche Entera");
        await AddBarcodeAsync(connection, otherTenant, other, "7861111111111", 1);
        Assert.Equal(other, (await SearchAsync(connection, otherTenant, "7861111111111")).Single().Id);
        Assert.Equal(first, (await SearchAsync(connection, tenantId, "7861111111111")).Single().Id);
    }

    [Fact]
    public async Task Purchase_search_leaves_out_services()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Purchase_search_leaves_out_services)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await CreateProductAsync(connection, tenantId, "Instalación de equipo");
        var goods = await CreateProductAsync(connection, tenantId, "Equipo de sonido");
        await connection.ExecuteAsync(
            "UPDATE dbo.products SET kind = 2 WHERE tenant_id = @TenantId AND id <> @Goods",
            new { TenantId = tenantId, Goods = goods });

        Assert.Equal(2, (await SearchAsync(connection, tenantId, "equipo", purpose: 1)).Count);
        Assert.Equal(goods, (await SearchAsync(connection, tenantId, "equipo", purpose: 2)).Single().Id);
    }

    [Fact]
    public async Task Removing_the_primary_code_promotes_the_next_one()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Removing_the_primary_code_promotes_the_next_one)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var productId = await CreateProductAsync(connection, tenantId, "Agua 500 ml");
        var primary = await AddBarcodeAsync(connection, tenantId, productId, "7862222222222", 1);
        await AddBarcodeAsync(connection, tenantId, productId, "17862222222229", 24);

        await connection.ExecuteAsync(
            "dbo.sp_product_barcode_remove",
            new { p_tenant_id = tenantId, product_id = productId, barcode_id = primary },
            commandType: CommandType.StoredProcedure);

        var left = (await connection.QueryAsync<BarcodeRow>(
            "dbo.sp_product_barcode_list",
            new { p_tenant_id = tenantId, product_id = productId },
            commandType: CommandType.StoredProcedure)).Single();

        Assert.Equal("17862222222229", left.Barcode);
        Assert.True(left.IsPrimary);
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<Guid> CreateProductAsync(SqlConnection connection, Guid tenantId, string name)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("name", name);
        arguments.Add("base_price", 1.00m);
        arguments.Add("created_by", Guid.NewGuid());

        var row = await connection.QuerySingleAsync<BarcodeRow>(
            "dbo.sp_product_create", arguments, commandType: CommandType.StoredProcedure);
        return row.Id;
    }

    private static async Task<Guid> AddBarcodeAsync(SqlConnection connection, Guid tenantId, Guid productId, string barcode, int units)
    {
        var row = await connection.QuerySingleAsync<BarcodeRow>(
            "dbo.sp_product_barcode_add",
            new { p_tenant_id = tenantId, product_id = productId, barcode, units },
            commandType: CommandType.StoredProcedure);
        return row.Id;
    }

    private static async Task<List<SearchRow>> SearchAsync(SqlConnection connection, Guid tenantId, string term, byte purpose = 1)
        => (await connection.QueryAsync<SearchRow>(
            "dbo.sp_product_search",
            new { p_tenant_id = tenantId, term, max_rows = 10, purpose },
            commandType: CommandType.StoredProcedure)).ToList();

    private sealed class BarcodeRow
    {
        public Guid Id { get; set; }

        public string? Barcode { get; set; }

        public bool IsPrimary { get; set; }
    }

    private sealed class SearchRow
    {
        public Guid Id { get; set; }

        public string? Code { get; set; }

        public string? Barcode { get; set; }

        public int? MatchedUnits { get; set; }

        public int? UnitsPerPackage { get; set; }

        public bool IsExactMatch { get; set; }
    }
}
