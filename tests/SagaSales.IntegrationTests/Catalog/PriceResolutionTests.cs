using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Catalog;

/// <summary>
/// The pricing rule of D5 / FR-007b: a specific price wins, and when there is none the base price
/// applies — with the source reported so the caller can tell the buyer which price was used.
/// Requires <c>scripts/apply-db.ps1 -Service catalog</c> and <c>SAGASALES_TEST_SQL</c>.
/// </summary>
public class PriceResolutionTests
{
    private const short Retail = 1;
    private const short Wholesale = 2;
    private const short SourceBase = 1;
    private const short SourceList = 2;

    [Fact]
    public async Task Resolve_falls_back_to_the_base_price_when_no_list_price_exists()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Resolve_falls_back_to_the_base_price_when_no_list_price_exists)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();

        var productId = await CreateProductAsync(connection, tenantId, "Base only", 100m);

        var quote = await ResolveAsync(connection, tenantId, productId, Wholesale);

        Assert.Equal(100m, quote.Price);
        Assert.Equal(SourceBase, quote.Source);
    }

    [Fact]
    public async Task Resolve_uses_the_list_price_for_the_asking_customer_type_only()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Resolve_uses_the_list_price_for_the_asking_customer_type_only)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();

        var productId = await CreateProductAsync(connection, tenantId, "Two prices", 100m);

        await connection.QuerySingleAsync<PriceRow>(
            "dbo.sp_price_set",
            new DynamicParameters()
                .AddTenant(tenantId)
                .AddProduct(productId)
                .WithType(Wholesale)
                .WithPrice(80m),
            commandType: CommandType.StoredProcedure);

        var wholesale = await ResolveAsync(connection, tenantId, productId, Wholesale);
        var retail = await ResolveAsync(connection, tenantId, productId, Retail);

        Assert.Equal(80m, wholesale.Price);
        Assert.Equal(SourceList, wholesale.Source);

        Assert.Equal(100m, retail.Price);
        Assert.Equal(SourceBase, retail.Source);
    }

    [Fact]
    public async Task Resolve_refuses_a_product_from_another_tenant()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Resolve_refuses_a_product_from_another_tenant)))
        {
            return;
        }

        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        await using var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();

        var productId = await CreateProductAsync(connection, owner, "Private product", 50m);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            ResolveAsync(connection, stranger, productId, Retail));

        Assert.Contains("NOT_FOUND", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> CreateProductAsync(
        SqlConnection connection,
        Guid tenantId,
        string name,
        decimal basePrice)
    {
        var arguments = new DynamicParameters()
            .AddTenant(tenantId)
            .WithName(name)
            .WithBasePrice(basePrice)
            .WithStock(10)
            .WithCreatedBy(Guid.NewGuid());

        var row = await connection.QuerySingleAsync<ProductRow>(
            "dbo.sp_product_create", arguments, commandType: CommandType.StoredProcedure);

        return row.Id;
    }

    private static Task<PriceRow> ResolveAsync(SqlConnection connection, Guid tenantId, Guid productId, short type)
        => connection.QuerySingleAsync<PriceRow>(
            "dbo.sp_price_resolve",
            new DynamicParameters().AddTenant(tenantId).AddProduct(productId).WithType(type),
            commandType: CommandType.StoredProcedure);

    private sealed class ProductRow
    {
        public Guid Id { get; set; }
    }

    private sealed class PriceRow
    {
        public Guid ProductId { get; set; }

        public short CustomerType { get; set; }

        public decimal Price { get; set; }

        public short Source { get; set; }
    }
}

internal static class CatalogProcedureArguments
{
    public static DynamicParameters AddTenant(this DynamicParameters parameters, Guid tenantId)
    {
        parameters.Add("p_tenant_id", tenantId);
        return parameters;
    }

    public static DynamicParameters AddProduct(this DynamicParameters parameters, Guid productId)
    {
        parameters.Add("product_id", productId);
        return parameters;
    }

    public static DynamicParameters WithType(this DynamicParameters parameters, short customerType)
    {
        parameters.Add("customer_type", customerType);
        return parameters;
    }

    public static DynamicParameters WithPrice(this DynamicParameters parameters, decimal price)
    {
        parameters.Add("price", price);
        return parameters;
    }

    public static DynamicParameters WithBasePrice(this DynamicParameters parameters, decimal basePrice)
    {
        parameters.Add("base_price", basePrice);
        return parameters;
    }

    public static DynamicParameters WithName(this DynamicParameters parameters, string name)
    {
        parameters.Add("name", name);
        return parameters;
    }

    public static DynamicParameters WithStock(this DynamicParameters parameters, int stock)
    {
        parameters.Add("stock_quantity", stock);
        return parameters;
    }

    public static DynamicParameters WithCreatedBy(this DynamicParameters parameters, Guid userId)
    {
        parameters.Add("created_by", userId);
        return parameters;
    }
}
