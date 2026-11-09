using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Catalog;

/// <summary>
/// Spec 004 against db/catalog/V024 and V025: inventory report and reorder settings.
/// Requires <c>SAGASALES_TEST_SQL</c> on <c>saga_catalog_test</c>.
/// </summary>
public class InventoryReportTests
{
    [Fact]
    public async Task Value_and_reorder_mark_follow_cost_stock_and_level()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Value_and_reorder_mark_follow_cost_stock_and_level)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var low = await CreateProductAsync(connection, tenantId, "Artículo D", cost: 10m, stock: 20);
        var high = await CreateProductAsync(connection, tenantId, "Artículo E", cost: 20m, stock: 200);
        await SetSettingsAsync(connection, tenantId, low, code: "D123", reorderLevel: 50);
        await SetSettingsAsync(connection, tenantId, high, code: "E123", reorderLevel: 50);

        var rows = await ReportAsync(connection, tenantId);

        Assert.Equal(low, rows[0].ProductId);          // what needs restocking comes first
        Assert.True(rows[0].NeedsReorder);
        Assert.Equal(200.00m, rows[0].InventoryValue);
        Assert.False(rows[1].NeedsReorder);
        Assert.Equal(4000.00m, rows[1].InventoryValue);
    }

    [Fact]
    public async Task An_unknown_cost_gives_no_value_and_no_level_never_asks_to_reorder()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(An_unknown_cost_gives_no_value_and_no_level_never_asks_to_reorder)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await CreateProductAsync(connection, tenantId, "Nunca comprado", cost: null, stock: 0);

        var row = (await ReportAsync(connection, tenantId)).Single();

        Assert.Null(row.UnitCost);
        Assert.Null(row.InventoryValue);
        Assert.False(row.NeedsReorder);
    }

    [Fact]
    public async Task A_branch_filter_uses_that_branch_stock_and_sales()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_branch_filter_uses_that_branch_stock_and_sales)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var productId = await CreateProductAsync(connection, tenantId, "Coca Cola", cost: 0.30m, stock: 100);
        await SetSettingsAsync(connection, tenantId, productId, code: null, reorderLevel: 10);

        var branchId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO dbo.branches (id, tenant_id, name) VALUES (@BranchId, @TenantId, 'Sucursal Norte');" +
            "INSERT INTO dbo.product_stock (product_id, branch_id, tenant_id, quantity) VALUES (@ProductId, @BranchId, @TenantId, 5);" +
            "INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, stock_after, reference_type, occurred_at, branch_id) " +
            "VALUES (@TenantId, @ProductId, 2, -1, 5, N'order', '2026-11-01T10:00:00', @BranchId);",
            new { BranchId = branchId, TenantId = tenantId, ProductId = productId });

        var all = (await ReportAsync(connection, tenantId)).Single();
        var branch = (await ReportAsync(connection, tenantId, branchId)).Single();

        Assert.Equal(100, all.Stock);
        Assert.False(all.NeedsReorder);
        Assert.Equal(5, branch.Stock);
        Assert.True(branch.NeedsReorder);
        Assert.Equal(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Unspecified), branch.LastSaleAt);
    }

    [Fact]
    public async Task The_last_supplier_is_the_one_of_the_last_received_purchase()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_last_supplier_is_the_one_of_the_last_received_purchase)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var productId = await CreateProductAsync(connection, tenantId, "Coca Cola", cost: 0.30m, stock: 10);
        await InsertReceivedPurchaseAsync(connection, tenantId, productId, "Proveedor Antiguo", "2026-10-01T09:00:00");
        await InsertReceivedPurchaseAsync(connection, tenantId, productId, "Proveedor Nuevo", "2026-11-01T09:00:00");

        var row = (await ReportAsync(connection, tenantId)).Single();

        Assert.Equal("Proveedor Nuevo", row.LastSupplier);
    }

    [Fact]
    public async Task Settings_do_not_touch_price_or_stock()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Settings_do_not_touch_price_or_stock)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var productId = await CreateProductAsync(connection, tenantId, "Coca Cola", cost: 0.30m, stock: 37);

        await SetSettingsAsync(connection, tenantId, productId, code: "CC500", reorderLevel: 12,
            manufacturer: "Coca-Cola Company", reorderQuantity: 48, leadTimeDays: 3, notes: "Pedir los lunes");

        var stored = await connection.QuerySingleAsync<(decimal Price, int Stock)>(
            "SELECT base_price, stock_quantity FROM dbo.products WHERE id = @Id", new { Id = productId });
        var row = (await ReportAsync(connection, tenantId)).Single();

        Assert.Equal(0.40m, stored.Price);
        Assert.Equal(37, stored.Stock);
        Assert.Equal("CC500", row.Code);
        Assert.Equal("Coca-Cola Company", row.Manufacturer);
        Assert.Equal(48, row.ReorderQuantity);
        Assert.Equal(3, row.LeadTimeDays);
        Assert.Equal("Pedir los lunes", row.Notes);
    }

    [Fact]
    public async Task A_code_is_unique_inside_a_store_only()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_code_is_unique_inside_a_store_only)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var first = await CreateProductAsync(connection, tenantId, "Uno", cost: null, stock: 0);
        var second = await CreateProductAsync(connection, tenantId, "Dos", cost: null, stock: 0);
        await SetSettingsAsync(connection, tenantId, first, code: "A123", reorderLevel: null);

        var duplicate = await Assert.ThrowsAsync<SqlException>(() =>
            SetSettingsAsync(connection, tenantId, second, code: "A123", reorderLevel: null));
        Assert.Contains("CONFLICT", duplicate.Message, StringComparison.Ordinal);

        var otherTenant = Guid.NewGuid();
        var elsewhere = await CreateProductAsync(connection, otherTenant, "Tres", cost: null, stock: 0);
        await SetSettingsAsync(connection, otherTenant, elsewhere, code: "A123", reorderLevel: null);
    }

    [Fact]
    public async Task Negative_numbers_are_refused()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Negative_numbers_are_refused)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        var productId = await CreateProductAsync(connection, tenantId, "Uno", cost: null, stock: 0);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            SetSettingsAsync(connection, tenantId, productId, code: null, reorderLevel: -1));

        Assert.Contains("VALIDATION_ERROR", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Another_store_sees_nothing()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Another_store_sees_nothing)))
        {
            return;
        }

        await using var connection = await OpenAsync();
        await CreateProductAsync(connection, Guid.NewGuid(), "Privado", cost: 1m, stock: 1);

        Assert.Empty(await ReportAsync(connection, Guid.NewGuid()));
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<Guid> CreateProductAsync(SqlConnection connection, Guid tenantId, string name, decimal? cost, int stock)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("name", name);
        arguments.Add("base_price", 0.40m);
        arguments.Add("created_by", Guid.NewGuid());

        var row = await connection.QuerySingleAsync<IdRow>(
            "dbo.sp_product_create", arguments, commandType: CommandType.StoredProcedure);

        await connection.ExecuteAsync(
            "UPDATE dbo.products SET cost = @Cost, stock_quantity = @Stock WHERE id = @Id AND tenant_id = @TenantId",
            new { Cost = cost, Stock = stock, row.Id, TenantId = tenantId });

        return row.Id;
    }

    private static Task SetSettingsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid productId,
        string? code,
        int? reorderLevel,
        string? manufacturer = null,
        int? reorderQuantity = null,
        int? leadTimeDays = null,
        string? notes = null)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("product_id", productId);
        arguments.Add("code", code);
        arguments.Add("manufacturer", manufacturer);
        arguments.Add("reorder_level", reorderLevel);
        arguments.Add("reorder_quantity", reorderQuantity);
        arguments.Add("lead_time_days", leadTimeDays);
        arguments.Add("notes", notes);

        return connection.ExecuteAsync(
            "dbo.sp_product_set_inventory_settings", arguments, commandType: CommandType.StoredProcedure);
    }

    private static async Task InsertReceivedPurchaseAsync(
        SqlConnection connection, Guid tenantId, Guid productId, string supplier, string receivedAt)
    {
        await connection.ExecuteAsync(
            // T-SQL variable names ignore case: the local ids must not be called like the Dapper parameters.
            "DECLARE @new_supplier_id uniqueidentifier = NEWID(), @new_purchase_id uniqueidentifier = NEWID();" +
            "INSERT INTO dbo.suppliers (id, tenant_id, name) VALUES (@new_supplier_id, @TenantId, @SupplierName);" +
            "INSERT INTO dbo.purchases (id, tenant_id, supplier_id, status, received_at, created_by) " +
            "VALUES (@new_purchase_id, @TenantId, @new_supplier_id, 2, @ReceivedAt, NEWID());" +
            "INSERT INTO dbo.purchase_lines (tenant_id, purchase_id, product_id, product_name, quantity, unit_cost, effective_unit_cost, line_total) " +
            "VALUES (@TenantId, @new_purchase_id, @ProductId, N'Coca Cola', 10, 0.30, 0.30, 3.00);",
            new { TenantId = tenantId, SupplierName = supplier, ReceivedAt = receivedAt, ProductId = productId });
    }

    private static async Task<List<ReportRow>> ReportAsync(SqlConnection connection, Guid tenantId, Guid? branchId = null)
    {
        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("branch_id", branchId);

        var rows = await connection.QueryAsync<ReportRow>(
            "dbo.sp_inventory_report", arguments, commandType: CommandType.StoredProcedure);
        return rows.AsList();
    }

    private sealed class IdRow
    {
        public Guid Id { get; set; }
    }

    private sealed class ReportRow
    {
        public Guid ProductId { get; set; }

        public string? Code { get; set; }

        public string? Manufacturer { get; set; }

        public decimal? UnitCost { get; set; }

        public int Stock { get; set; }

        public decimal? InventoryValue { get; set; }

        public int? ReorderQuantity { get; set; }

        public int? LeadTimeDays { get; set; }

        public DateTime? LastSaleAt { get; set; }

        public string? Notes { get; set; }

        public string? LastSupplier { get; set; }

        public bool NeedsReorder { get; set; }
    }
}
