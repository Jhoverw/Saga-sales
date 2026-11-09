using System.Net;
using System.Net.Http.Json;
using System.Text;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Inventory;

/// <summary>
/// Spec 004 through the gateway: reorder settings and the inventory control report.
/// Requires <c>SAGASALES_GATEWAY_URL</c> and a running stack.
/// </summary>
public class InventoryReportContractTests
{
    [Fact]
    public async Task A_product_below_its_reorder_level_is_reported_first_and_marked()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_product_below_its_reorder_level_is_reported_first_and_marked)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);
        var product = await CreateProductAsync(session, "Coca Cola 500 ml");

        using var settings = await session.SendAsync(
            HttpMethod.Put,
            $"api/inventory/products/{product.Id}/settings",
            new InventorySettingsRequest { Code = "CC500", Manufacturer = "Coca-Cola", ReorderLevel = 50, ReorderQuantity = 24, LeadTimeDays = 3 });
        Assert.Equal(HttpStatusCode.NoContent, settings.StatusCode);

        using var response = await session.SendAsync(HttpMethod.Get, "api/inventory/report");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rows = await response.Content.ReadFromJsonAsync<List<InventoryReportRowDto>>();
        Assert.NotNull(rows);
        var row = rows!.Single(r => r.ProductId == product.Id);
        Assert.True(row.NeedsReorder);
        Assert.Equal("CC500", row.Code);
        Assert.Equal(24, row.ReorderQuantity);
    }

    [Fact]
    public async Task A_repeated_code_returns_409_and_a_negative_level_returns_400()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_repeated_code_returns_409_and_a_negative_level_returns_400)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);
        var first = await CreateProductAsync(session, "Uno");
        var second = await CreateProductAsync(session, "Dos");

        using var ok = await session.SendAsync(HttpMethod.Put, $"api/inventory/products/{first.Id}/settings",
            new InventorySettingsRequest { Code = "A123" });
        using var duplicate = await session.SendAsync(HttpMethod.Put, $"api/inventory/products/{second.Id}/settings",
            new InventorySettingsRequest { Code = "A123" });
        using var negative = await session.SendAsync(HttpMethod.Put, $"api/inventory/products/{second.Id}/settings",
            new InventorySettingsRequest { ReorderLevel = -1 });

        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
    }

    [Fact]
    public async Task The_report_downloads_as_csv_with_bom_and_semicolons()
    {
        if (Gateway.TryAnnounceSkip(nameof(The_report_downloads_as_csv_with_bom_and_semicolons)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);
        await CreateProductAsync(session, "Coca Cola 500 ml");

        using var response = await session.SendAsync(HttpMethod.Get, "api/inventory/report.csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "UTF-8 BOM expected");
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.StartsWith("Reponer;N.º de artículo;Nombre;", text, StringComparison.Ordinal);
        Assert.Contains("Coca Cola 500 ml", text, StringComparison.Ordinal);
    }

    private static async Task<ProductDto> CreateProductAsync(ContractSession session, string name)
    {
        using var response = await session.SendAsync(HttpMethod.Post, "api/products", new CreateProductRequest
        {
            Name = name,
            Price = 0.40m,
            StockQuantity = 0,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(product);
        return product!;
    }
}
