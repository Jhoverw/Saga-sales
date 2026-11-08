using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SagaSales.IntegrationTests.Sales;

/// <summary>
/// V024: VAT is rounded per line and summed, so the counter screen and the server agree to the cent.
/// Two lines of 0.30 at 15 % are 0.045 each, which rounds to 0.05 each: 0.10 in total. Rounding over the
/// subtotal would give 0.09. Requires <c>SAGASALES_TEST_SQL</c> and V024 on <c>saga_sales_test</c>.
/// </summary>
public class OrderTaxPerLineTests
{
    [Fact]
    public async Task Vat_is_rounded_per_line_and_summed()
    {
        if (TestDatabase.TryAnnounceSkip(nameof(Vat_is_rounded_per_line_and_summed)))
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        await using var connection = new SqlConnection(TestDatabase.ForService("sales"));
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            "INSERT INTO dbo.tax_rates (tenant_id, rate_percentage, start_date, end_date, description) " +
            "VALUES (@TenantId, 0.15, '2000-01-01', NULL, 'IVA per-line test');",
            new { TenantId = tenantId });

        var lines = JsonSerializer.Serialize(new object[]
        {
            new { productId = Guid.NewGuid(), productName = "Cent A", quantity = 1, unitPrice = 0.30m, priceSource = 1, stockAtPurchase = 9 },
            new { productId = Guid.NewGuid(), productName = "Cent B", quantity = 1, unitPrice = 0.30m, priceSource = 1, stockAtPurchase = 9 },
        });

        var parameters = new DynamicParameters();
        parameters.Add("p_tenant_id", tenantId);
        parameters.Add("customer_id", Guid.NewGuid());
        parameters.Add("shipping_address", "Calle 1, Quito");
        parameters.Add("created_by", Guid.NewGuid());
        parameters.Add("lines_json", lines);
        parameters.Add("notes", null);

        var order = await connection.QuerySingleAsync<OrderTaxRow>(
            "dbo.sp_order_create", parameters, commandType: CommandType.StoredProcedure);

        Assert.Equal(0.60m, order.Subtotal);
        Assert.Equal(0.10m, order.TaxAmount);
    }

    private sealed class OrderTaxRow
    {
        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }
    }
}
