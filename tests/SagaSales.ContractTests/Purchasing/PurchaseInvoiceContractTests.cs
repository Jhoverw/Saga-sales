using System.Net;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Purchasing;

/// <summary>
/// Contract of spec 009: a product created from the invoice line, the purchase entered like the supplier invoice
/// (box → units), the duplicate document refused and the credit turned into a payable. Requires a running stack.
/// </summary>
public class PurchaseInvoiceContractTests
{
    [Fact]
    public async Task An_invoice_with_a_new_product_by_the_box_on_credit_flows_end_to_end()
    {
        if (Gateway.TryAnnounceSkip(nameof(An_invoice_with_a_new_product_by_the_box_on_credit_flows_end_to_end)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);
        var supplier = await CreateSupplierAsync(session);
        var code = "GA" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        // 1. Producto nuevo desde la línea, con código y caja de 12.
        using var created = await session.SendAsync(HttpMethod.Post, "api/purchases/products", new CreatePurchaseProductRequest
        {
            Code = code,
            Name = "Galletas Amor " + code,
            SalePrice = 0.85m,
            IsTaxable = true,
            UnitsPerPackage = 12,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = (await created.Content.ReadFromJsonAsync<PurchaseProductDto>())!;

        using var sameCode = await session.SendAsync(HttpMethod.Post, "api/purchases/products", new CreatePurchaseProductRequest
        {
            Code = code,
            Name = "Otro",
            SalePrice = 1m,
        });
        Assert.Equal(HttpStatusCode.Conflict, sameCode.StatusCode);

        // 2. Se encuentra por código con su empaque.
        using var search = await session.SendAsync(HttpMethod.Get, $"api/purchases/products?term={code}");
        var found = await search.Content.ReadFromJsonAsync<List<PurchaseProductDto>>();
        Assert.Equal(12, Assert.Single(found!).UnitsPerPackage);

        // 3. Factura: 2 cajas a 6.00 + IVA, a crédito.
        var request = Invoice(supplier.Id, product.Id, "001-001-" + Random.Shared.Next(1, 999_999_999).ToString("D9"));
        using var invoice = await session.SendAsync(HttpMethod.Post, "api/purchases/invoice", request);
        Assert.Equal(HttpStatusCode.Created, invoice.StatusCode);
        var purchase = (await invoice.Content.ReadFromJsonAsync<PurchaseDto>())!;
        Assert.Equal(13.80m, purchase.Total);
        Assert.Equal(2, purchase.Status);

        using var productAfter = await session.SendAsync(HttpMethod.Get, $"api/products/{product.Id}");
        Assert.Equal(24, (await productAfter.Content.ReadFromJsonAsync<ProductDto>())!.StockQuantity);

        using var again = await session.SendAsync(HttpMethod.Post, "api/purchases/invoice", request);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // 4. Cuenta por pagar y abonos.
        using var list = await session.SendAsync(HttpMethod.Get, "api/purchases/payables");
        var payable = Assert.Single((await list.Content.ReadFromJsonAsync<List<PayableDto>>())!, p => p.PurchaseId == purchase.Id);
        Assert.Equal(13.80m, payable.Balance);

        using var pay = await session.SendAsync(HttpMethod.Post, $"api/purchases/payables/{payable.Id}/payments",
            new PayablePaymentRequest { Amount = 5m, PaidAt = DateOnly.FromDateTime(DateTime.Today), Method = 2, Reference = "TRX-9" });
        Assert.Equal(HttpStatusCode.NoContent, pay.StatusCode);

        using var over = await session.SendAsync(HttpMethod.Post, $"api/purchases/payables/{payable.Id}/payments",
            new PayablePaymentRequest { Amount = 100m, PaidAt = DateOnly.FromDateTime(DateTime.Today) });
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);

        using var payments = await session.SendAsync(HttpMethod.Get, $"api/purchases/payables/{payable.Id}/payments");
        Assert.Single((await payments.Content.ReadFromJsonAsync<List<PayablePaymentDto>>())!);
    }

    [Fact]
    public async Task A_bad_invoice_number_or_a_credit_without_due_date_returns_400()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_bad_invoice_number_or_a_credit_without_due_date_returns_400)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);
        var supplier = await CreateSupplierAsync(session);
        var product = Guid.NewGuid();

        var badNumber = Invoice(supplier.Id, product, "123");
        using var first = await session.SendAsync(HttpMethod.Post, "api/purchases/invoice", badNumber);
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);

        var noDue = Invoice(supplier.Id, product, "001-001-000000001");
        noDue.CreditDueAt = null;
        using var second = await session.SendAsync(HttpMethod.Post, "api/purchases/invoice", noDue);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    private static PurchaseInvoiceRequest Invoice(Guid supplierId, Guid productId, string number) => new()
    {
        SupplierId = supplierId,
        ReceiveNow = true,
        DocumentType = 1,
        DocumentNumber = number,
        IssuedAt = DateOnly.FromDateTime(DateTime.Today),
        PaymentMethod = 20,
        IsCredit = true,
        CreditDueAt = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
        VatRate = 0.15m,
        Lines =
        [
            new PurchaseInvoiceLineRequest
            {
                ProductId = productId,
                ProductName = "Galletas Amor",
                Quantity = 2,
                Package = "caja",
                UnitsPerPackage = 12,
                UnitPrice = 6.00m,
                Taxable = true,
            },
        ],
    };

    private static async Task<SupplierDto> CreateSupplierAsync(ContractSession session)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var response = await session.SendAsync(HttpMethod.Post, "api/suppliers", new CreateSupplierRequest
        {
            Name = "Proveedor " + suffix,
            TaxId = "179" + suffix,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SupplierDto>())!;
    }
}
