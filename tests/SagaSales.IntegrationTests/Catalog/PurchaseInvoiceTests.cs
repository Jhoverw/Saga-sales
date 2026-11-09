using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Catalog;

/// <summary>
/// Spec 009 against db/catalog/V026: purchase entered like the supplier invoice, box size saved on the product,
/// duplicate documents refused and credit purchases turned into payables with partial payments.
/// Requires <c>SAGASALES_TEST_SQL</c> on <c>saga_catalog_test</c>.
/// </summary>
public class PurchaseInvoiceTests
{
    [Fact]
    public async Task A_box_purchase_enters_units_saves_the_box_and_sets_the_unit_cost()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_box_purchase_enters_units_saves_the_box_and_sets_the_unit_cost)))
        {
            return;
        }

        var fixture = await ArrangeAsync();
        await using var connection = fixture.Connection;

        // 2 cajas x 12 a 6.00 + IVA 15 % = 13.80 → 24 unidades a 0.575.
        var purchase = await CreateInvoiceAsync(connection, fixture, "001-001-000000101", isCredit: false);

        Assert.Equal((short)2, purchase.Status);
        Assert.Equal(13.80m, purchase.Total);

        var product = await connection.QuerySingleAsync<ProductRow>(
            "SELECT stock_quantity AS StockQuantity, units_per_package AS UnitsPerPackage, cost AS Cost FROM dbo.products WHERE id = @Id",
            new { Id = fixture.ProductId });

        Assert.Equal(24, product.StockQuantity);
        Assert.Equal(12, product.UnitsPerPackage);
        Assert.Equal(0.58m, Math.Round(product.Cost ?? 0, 2));

        var line = await connection.QuerySingleAsync<LineRow>(
            "SELECT quantity AS Quantity, invoice_quantity AS InvoiceQuantity, units_per_package AS UnitsPerPackage, " +
            "invoice_unit_price AS InvoiceUnitPrice FROM dbo.purchase_lines WHERE purchase_id = @Id",
            new { purchase.Id });

        Assert.Equal(24, line.Quantity);
        Assert.Equal(2m, line.InvoiceQuantity);
        Assert.Equal(12, line.UnitsPerPackage);
        Assert.Equal(6.00m, line.InvoiceUnitPrice);
    }

    [Fact]
    public async Task The_same_supplier_document_cannot_be_entered_twice()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(The_same_supplier_document_cannot_be_entered_twice)))
        {
            return;
        }

        var fixture = await ArrangeAsync();
        await using var connection = fixture.Connection;
        await CreateInvoiceAsync(connection, fixture, "001-001-000000202", isCredit: false);

        var duplicate = await Assert.ThrowsAsync<SqlException>(
            () => CreateInvoiceAsync(connection, fixture, "001-001-000000202", isCredit: false));

        Assert.Contains("CONFLICT:", duplicate.Message);
    }

    [Fact]
    public async Task A_credit_purchase_becomes_a_payable_paid_in_installments()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(A_credit_purchase_becomes_a_payable_paid_in_installments)))
        {
            return;
        }

        var fixture = await ArrangeAsync();
        await using var connection = fixture.Connection;
        var purchase = await CreateInvoiceAsync(connection, fixture, "001-001-000000303", isCredit: true);

        var payable = (await ListPayablesAsync(connection, fixture.TenantId, includePaid: false)).Single();
        Assert.Equal(purchase.Id, payable.PurchaseId);
        Assert.Equal(13.80m, payable.Balance);
        Assert.Equal(1, payable.Status);

        await PayAsync(connection, fixture.TenantId, payable.Id, 5.00m);

        var over = await Assert.ThrowsAsync<SqlException>(() => PayAsync(connection, fixture.TenantId, payable.Id, 9.00m));
        Assert.Contains("CONFLICT:", over.Message);

        await PayAsync(connection, fixture.TenantId, payable.Id, 8.80m);

        Assert.Empty(await ListPayablesAsync(connection, fixture.TenantId, includePaid: false));
        var paid = (await ListPayablesAsync(connection, fixture.TenantId, includePaid: true)).Single();
        Assert.Equal(0m, paid.Balance);
        Assert.Equal(2, paid.Status);

        var payments = await connection.QueryAsync(
            "dbo.sp_payable_payments_list",
            new { p_tenant_id = fixture.TenantId, payable_id = payable.Id },
            commandType: CommandType.StoredProcedure);
        Assert.Equal(2, payments.Count());
    }

    [Fact]
    public async Task Another_store_cannot_see_or_pay_the_payable()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Another_store_cannot_see_or_pay_the_payable)))
        {
            return;
        }

        var fixture = await ArrangeAsync();
        await using var connection = fixture.Connection;
        await CreateInvoiceAsync(connection, fixture, "001-001-000000404", isCredit: true);
        var payable = (await ListPayablesAsync(connection, fixture.TenantId, includePaid: false)).Single();

        var otherTenant = Guid.NewGuid();
        Assert.Empty(await ListPayablesAsync(connection, otherTenant, includePaid: true));

        var foreign = await Assert.ThrowsAsync<SqlException>(() => PayAsync(connection, otherTenant, payable.Id, 1.00m));
        Assert.Contains("NOT_FOUND:", foreign.Message);
    }

    private static async Task<Fixture> ArrangeAsync()
    {
        var tenantId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var connection = new SqlConnection(TestDatabase.ForService("catalog"));
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            "INSERT INTO dbo.suppliers (id, tenant_id, name) VALUES (@SupplierId, @TenantId, N'Distribuidora Andina');" +
            "INSERT INTO dbo.branches (id, tenant_id, name, is_default) VALUES (@BranchId, @TenantId, N'Principal', 1);",
            new { SupplierId = supplierId, BranchId = branchId, TenantId = tenantId });

        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", tenantId);
        arguments.Add("name", "Galletas Amor");
        arguments.Add("base_price", 0.85m);
        arguments.Add("created_by", Guid.NewGuid());
        var product = await connection.QuerySingleAsync<IdRow>("dbo.sp_product_create", arguments, commandType: CommandType.StoredProcedure);

        return new Fixture(connection, tenantId, supplierId, branchId, product.Id);
    }

    private static async Task<PurchaseRow> CreateInvoiceAsync(SqlConnection connection, Fixture fixture, string number, bool isCredit)
    {
        var lines = JsonSerializer.Serialize(new[]
        {
            new
            {
                productId = fixture.ProductId,
                productName = "Galletas Amor",
                stockQuantity = 24,
                invoiceQuantity = 2m,
                packageLabel = "caja x12",
                unitsPerPackage = 12,
                invoiceUnitPrice = 6.00m,
                discount = 0m,
                taxable = true,
                effectiveUnitCost = 0.575m,
                lineTotal = 13.80m,
                lotCode = (string?)null,
                expiresAt = (string?)null,
                savePackage = true,
            },
        });

        var arguments = new DynamicParameters();
        arguments.Add("p_tenant_id", fixture.TenantId);
        arguments.Add("supplier_id", fixture.SupplierId);
        arguments.Add("created_by", Guid.NewGuid());
        arguments.Add("branch_id", fixture.BranchId);
        arguments.Add("receive", true);
        arguments.Add("document_type", (short)1);
        arguments.Add("document_number", number);
        arguments.Add("issued_at", new DateTime(2026, 11, 2));
        arguments.Add("is_credit", isCredit);
        arguments.Add("credit_due_at", isCredit ? new DateTime(2026, 12, 2) : null);
        arguments.Add("vat_rate", 0.15m);
        arguments.Add("subtotal_zero", 0m);
        arguments.Add("subtotal_taxed", 12.00m);
        arguments.Add("tax_amount", 1.80m);
        arguments.Add("total", 13.80m);
        arguments.Add("lines_json", lines);

        // Una sola fila aunque reciba en el mismo paso.
        var rows = (await connection.QueryAsync<PurchaseRow>(
            "dbo.sp_purchase_create_invoice", arguments, commandType: CommandType.StoredProcedure)).ToList();
        return Assert.Single(rows);
    }

    private static async Task<List<PayableRow>> ListPayablesAsync(SqlConnection connection, Guid tenantId, bool includePaid)
        => (await connection.QueryAsync<PayableRow>(
            "dbo.sp_payable_list",
            new { p_tenant_id = tenantId, include_paid = includePaid },
            commandType: CommandType.StoredProcedure)).ToList();

    private static Task PayAsync(SqlConnection connection, Guid tenantId, Guid payableId, decimal amount)
        => connection.ExecuteAsync(
            "dbo.sp_payable_pay",
            new
            {
                p_tenant_id = tenantId,
                payable_id = payableId,
                amount,
                paid_at = new DateTime(2026, 11, 9),
                method = (short)2,
                reference = "TRX-1",
                created_by = Guid.NewGuid(),
            },
            commandType: CommandType.StoredProcedure);

    private sealed record Fixture(SqlConnection Connection, Guid TenantId, Guid SupplierId, Guid BranchId, Guid ProductId);

    private sealed class IdRow
    {
        public Guid Id { get; set; }
    }

    private sealed class PurchaseRow
    {
        public Guid Id { get; set; }

        public short Status { get; set; }

        public decimal Total { get; set; }
    }

    private sealed class ProductRow
    {
        public int StockQuantity { get; set; }

        public int? UnitsPerPackage { get; set; }

        public decimal? Cost { get; set; }
    }

    private sealed class LineRow
    {
        public int Quantity { get; set; }

        public decimal InvoiceQuantity { get; set; }

        public int UnitsPerPackage { get; set; }

        public decimal InvoiceUnitPrice { get; set; }
    }

    private sealed class PayableRow
    {
        public Guid Id { get; set; }

        public Guid PurchaseId { get; set; }

        public decimal Balance { get; set; }

        public int Status { get; set; }
    }
}
