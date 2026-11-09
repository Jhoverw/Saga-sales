using SagaSales.Catalog.Queries;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.UnitTests.Catalog;

/// <summary>Spec 006, FR-008: el cajero ve agotados y productos en su nivel de reorden, sin costos.</summary>
public class InventoryAlertsTests
{
    [Fact]
    public void Out_of_stock_first_then_closest_to_the_reorder_level()
    {
        var rows = new[]
        {
            Row("Arroz", stock: 8, reorder: 10, cost: 1.10m),
            Row("Coca Cola", stock: 0, reorder: null, cost: 0.30m),
            Row("Azúcar", stock: 3, reorder: 10, cost: 0.90m),
            Row("Sal", stock: 50, reorder: 10, cost: 0.20m),
            Row("Fideo", stock: 12, reorder: null, cost: 0.50m),
        };

        var alerts = GetInventoryAlertsQueryHandler.ToLowStock(rows, 50);

        Assert.Equal(new[] { "Coca Cola", "Azúcar", "Arroz" }, alerts.Select(a => a.Name));
        Assert.Equal(0, alerts[0].Stock);
    }

    private static InventoryReportRowDto Row(string name, int stock, int? reorder, decimal cost) => new()
    {
        ProductId = Guid.NewGuid(),
        Name = name,
        Stock = stock,
        ReorderLevel = reorder,
        UnitCost = cost,
    };
}
