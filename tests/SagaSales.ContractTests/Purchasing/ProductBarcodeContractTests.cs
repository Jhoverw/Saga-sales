using System.Net;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Purchasing;

/// <summary>
/// Contract of spec 010: the barcode captured while buying (new product from the invoice line, or an unknown code
/// associated as a box) is what the one search finds when selling. Requires a running stack.
/// </summary>
public class ProductBarcodeContractTests
{
    [Fact]
    public async Task A_code_captured_in_purchases_is_found_by_the_sales_search()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_code_captured_in_purchases_is_found_by_the_sales_search)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);
        var unitCode = "786" + Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);
        var boxCode = "1" + unitCode;

        // Compras: producto nuevo desde la línea con el código escaneado.
        using var created = await session.SendAsync(HttpMethod.Post, "api/purchases/products", new CreatePurchaseProductRequest
        {
            Name = "Atún en lata " + unitCode[^4..],
            SalePrice = 1.75m,
            IsTaxable = true,
            Barcode = unitCode,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = (await created.Content.ReadFromJsonAsync<PurchaseProductDto>())!;

        // Compras: un código desconocido (el de la caja) se asocia al producto.
        using var box = await session.SendAsync(HttpMethod.Post, $"api/products/{product.Id}/barcodes",
            new AddProductBarcodeRequest { Barcode = boxCode, Units = 48 });
        Assert.Equal(HttpStatusCode.Created, box.StatusCode);

        using var again = await session.SendAsync(HttpMethod.Post, $"api/products/{product.Id}/barcodes",
            new AddProductBarcodeRequest { Barcode = boxCode, Units = 48 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // Ventas: el lector manda el código y el buscador lo encuentra exacto.
        var unit = await SearchAsync(session, unitCode, "sale");
        Assert.True(unit.IsExactMatch);
        Assert.Equal(product.Id, unit.Id);
        Assert.Equal(1, unit.MatchedUnits);

        var fromBox = await SearchAsync(session, boxCode, "sale");
        Assert.Equal(48, fromBox.MatchedUnits);

        // Y por nombre, sin saber el código.
        using var byName = await session.SendAsync(HttpMethod.Get, $"api/products/search?term=atun%20{unitCode[^4..]}&purpose=purchase");
        var found = await byName.Content.ReadFromJsonAsync<List<ProductSearchResultDto>>();
        Assert.Contains(found!, p => p.Id == product.Id);

        using var list = await session.SendAsync(HttpMethod.Get, $"api/products/{product.Id}/barcodes");
        Assert.Equal(2, (await list.Content.ReadFromJsonAsync<List<ProductBarcodeDto>>())!.Count);
    }

    [Fact]
    public async Task A_barcode_with_spaces_returns_400()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_barcode_with_spaces_returns_400)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);
        using var created = await session.SendAsync(HttpMethod.Post, "api/purchases/products", new CreatePurchaseProductRequest
        {
            Name = "Producto " + Guid.NewGuid().ToString("N")[..6],
            SalePrice = 1m,
        });
        var product = (await created.Content.ReadFromJsonAsync<PurchaseProductDto>())!;

        using var response = await session.SendAsync(HttpMethod.Post, $"api/products/{product.Id}/barcodes",
            new AddProductBarcodeRequest { Barcode = "786 123", Units = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<ProductSearchResultDto> SearchAsync(ContractSession session, string term, string purpose)
    {
        using var response = await session.SendAsync(HttpMethod.Get, $"api/products/search?term={term}&purpose={purpose}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<ProductSearchResultDto>>();
        return Assert.Single(results!);
    }
}
