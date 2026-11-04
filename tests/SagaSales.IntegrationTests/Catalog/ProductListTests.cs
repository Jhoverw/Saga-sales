using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Catalog;

/// <summary>
/// FR-009: filtering happens before pagination, and the reported total is the filtered total.
/// Requires <c>SAGASALES_TEST_SQL</c> and the catalog schema applied.
/// </summary>
public class ProductListTests
{
    [Fact]
    public async Task List_filters_before_paging_and_reports_the_filtered_total()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(List_filters_before_paging_and_reports_the_filtered_total)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();

        var categoryId = await CreateCategoryAsync(connection, tenantId, "Electronics");

        // 30 products in the category plus 5 outside it: paging must only see the 30.
        for (var i = 1; i <= 30; i++)
        {
            await CreateProductAsync(connection, tenantId, $"In category {i:000}", categoryId);
        }

        for (var i = 1; i <= 5; i++)
        {
            await CreateProductAsync(connection, tenantId, $"Outside {i:000}", null);
        }

        var arguments = new DynamicParameters()
            .AddTenant(tenantId)
            .WithPage(2, 10)
            .WithCategory(categoryId);

        var rows = (await connection.QueryAsync<ProductListRow>(
            "dbo.sp_product_list", arguments, commandType: CommandType.StoredProcedure)).AsList();

        Assert.Equal(10, rows.Count);
        Assert.All(rows, r => Assert.Equal(30, r.TotalCount));
        Assert.All(rows, r => Assert.Equal(categoryId, r.CategoryId));
    }

    [Fact]
    public async Task List_excludes_retired_products()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(List_excludes_retired_products)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();

        var productId = await CreateProductAsync(connection, tenantId, "To retire", null);

        await connection.ExecuteAsync(
            "dbo.sp_product_retire",
            new DynamicParameters().AddTenant(tenantId).AddProduct(productId),
            commandType: CommandType.StoredProcedure);

        var rows = (await connection.QueryAsync<ProductListRow>(
            "dbo.sp_product_list",
            new DynamicParameters().AddTenant(tenantId).WithPage(1, 50),
            commandType: CommandType.StoredProcedure)).AsList();

        Assert.DoesNotContain(rows, r => r.Id == productId);
    }

    private static async Task<Guid> CreateCategoryAsync(SqlConnection connection, Guid tenantId, string name)
    {
        var arguments = new DynamicParameters().AddTenant(tenantId).WithName(name);
        arguments.Add("slug", null);
        arguments.Add("parent_id", null);

        var row = await connection.QuerySingleAsync<CategoryRow>(
            "dbo.sp_category_create", arguments, commandType: CommandType.StoredProcedure);

        return row.Id;
    }

    private static async Task<Guid> CreateProductAsync(
        SqlConnection connection,
        Guid tenantId,
        string name,
        Guid? categoryId)
    {
        var arguments = new DynamicParameters()
            .AddTenant(tenantId)
            .WithName(name)
            .WithBasePrice(10m)
            .WithStock(5)
            .WithCreatedBy(Guid.NewGuid());

        arguments.Add("category_id", categoryId);

        var row = await connection.QuerySingleAsync<ProductRow>(
            "dbo.sp_product_create", arguments, commandType: CommandType.StoredProcedure);

        return row.Id;
    }

    private sealed class ProductRow
    {
        public Guid Id { get; set; }
    }

    private sealed class CategoryRow
    {
        public Guid Id { get; set; }
    }

    private sealed class ProductListRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public Guid? CategoryId { get; set; }

        public int TotalCount { get; set; }
    }
}

internal static class CatalogListArguments
{
    public static DynamicParameters WithPage(this DynamicParameters parameters, int pageNumber, int pageSize)
    {
        parameters.Add("page_number", pageNumber);
        parameters.Add("page_size", pageSize);
        parameters.Add("search", null);
        parameters.Add("sort_by", "name");
        parameters.Add("descending", false);
        return parameters;
    }

    public static DynamicParameters WithCategory(this DynamicParameters parameters, Guid? categoryId)
    {
        parameters.Add("category_id", categoryId);
        return parameters;
    }
}
