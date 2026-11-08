using System.Net;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Purchasing;

/// <summary>
/// Contract of purchases (E-12): one document with many lines, received in the same call, and
/// receiving twice is refused. Requires a running stack.
/// </summary>
public class PurchasesContractTests
{
    [Fact]
    public async Task A_multi_line_purchase_is_received_and_keeps_its_lines()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_multi_line_purchase_is_received_and_keeps_its_lines)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);

        var supplier = await CreateSupplierAsync(session);
        var product = await CreateProductAsync(session, stock: 0);

        var purchase = await CreatePurchaseAsync(session, supplier.Id, product.Id, receiveNow: true);

        using var detail = await session.SendAsync(HttpMethod.Get, $"api/purchases/{purchase.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var document = await detail.Content.ReadFromJsonAsync<PurchaseDto>();
        Assert.NotNull(document);
        Assert.Equal(2, document!.Lines.Count);
        Assert.Equal(2, document.Status); // received

        using var productAfter = await session.SendAsync(HttpMethod.Get, $"api/products/{product.Id}");
        var updated = await productAfter.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal(5, updated!.StockQuantity);

        using var again = await session.SendAsync(HttpMethod.Post, $"api/purchases/{purchase.Id}/receive");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_draft_purchase_does_not_move_stock_until_it_is_received()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_draft_purchase_does_not_move_stock_until_it_is_received)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);

        var supplier = await CreateSupplierAsync(session);
        var product = await CreateProductAsync(session, stock: 0);

        var draft = await CreatePurchaseAsync(session, supplier.Id, product.Id, receiveNow: false);

        using var before = await session.SendAsync(HttpMethod.Get, $"api/products/{product.Id}");
        var untouched = await before.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal(0, untouched!.StockQuantity);

        using var received = await session.SendAsync(HttpMethod.Post, $"api/purchases/{draft.Id}/receive");
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);

        using var after = await session.SendAsync(HttpMethod.Get, $"api/products/{product.Id}");
        var stocked = await after.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal(5, stocked!.StockQuantity);
    }

    [Fact]
    public async Task A_line_with_zero_cost_returns_400()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_line_with_zero_cost_returns_400)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);

        var supplier = await CreateSupplierAsync(session);
        var product = await CreateProductAsync(session, stock: 0);

        using var response = await session.SendAsync(HttpMethod.Post, "api/purchases", new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            ReceiveNow = true,
            Lines =
            [
                new PurchaseLineRequest { ProductId = product.Id, Quantity = 1, UnitCost = 0m, CostIncludesTax = true },
            ],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<SupplierDto> CreateSupplierAsync(ContractSession session)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var response = await session.SendAsync(HttpMethod.Post, "api/suppliers", new CreateSupplierRequest
        {
            Name = "Proveedor " + suffix,
            TaxId = "179" + suffix,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var supplier = await response.Content.ReadFromJsonAsync<SupplierDto>();
        return supplier!;
    }

    private static async Task<ProductDto> CreateProductAsync(ContractSession session, int stock)
    {
        using var response = await session.SendAsync(HttpMethod.Post, "api/products", new CreateProductRequest
        {
            Name = "Producto compra " + Guid.NewGuid().ToString("N")[..6],
            Price = 1.00m,
            StockQuantity = stock,
            Currency = "USD",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        return product!;
    }

    private static async Task<PurchaseDto> CreatePurchaseAsync(
        ContractSession session,
        Guid supplierId,
        Guid productId,
        bool receiveNow)
    {
        using var response = await session.SendAsync(HttpMethod.Post, "api/purchases", new CreatePurchaseRequest
        {
            SupplierId = supplierId,
            ReceiveNow = receiveNow,
            SupplierInvoiceNumber = "F-" + Guid.NewGuid().ToString("N")[..6],
            Lines =
            [
                new PurchaseLineRequest { ProductId = productId, Quantity = 3, UnitCost = 0.50m, CostIncludesTax = true },
                new PurchaseLineRequest { ProductId = productId, Quantity = 2, UnitCost = 0.60m, CostIncludesTax = true },
            ],
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var purchase = await response.Content.ReadFromJsonAsync<PurchaseDto>();
        return purchase!;
    }
}
