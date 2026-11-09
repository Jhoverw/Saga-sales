using System.Globalization;
using SagaSales.Catalog.Services;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.UnitTests.Catalog;

/// <summary>Spec 004, FR-009: CSV with ';', decimal point (constitution v2.4.0) and quoted text.</summary>
public class InventoryReportCsvTests
{
    private const string Header =
        "Reponer;N.º de artículo;Nombre;Fabricante;Descripción;Costo por artículo;Cantidad de existencias;" +
        "Valor del inventario;Nivel de reposición;Días por reposición;Fecha de última venta;" +
        "Cantidad de reposición;Notas;Último proveedor";

    [Fact]
    public void The_first_line_is_the_header()
    {
        var csv = InventoryReportCsv.Build([]);

        Assert.Equal(Header, csv.Split("\r\n")[0]);
    }

    [Fact]
    public void A_row_is_written_with_the_point_and_the_reorder_mark()
    {
        var csv = InventoryReportCsv.Build([Row()]);

        Assert.Equal(
            "REPONER;D123;Artículo D;Cole;Descripción D;10.00;20;200.00;50;14;2026-11-01;10;;Proveedor X",
            csv.Split("\r\n")[1]);
    }

    [Fact]
    public void An_unknown_cost_leaves_cost_and_value_empty()
    {
        var row = Row();
        row.UnitCost = null;
        row.InventoryValue = null;
        row.NeedsReorder = false;

        var line = InventoryReportCsv.Build([row]).Split("\r\n")[1];

        Assert.StartsWith("OK;D123;Artículo D;Cole;Descripción D;;20;;50;", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_with_separators_or_quotes_is_quoted()
    {
        var row = Row();
        row.Name = "Cola; \"light\"";

        var line = InventoryReportCsv.Build([row]).Split("\r\n")[1];

        Assert.Contains(";\"Cola; \"\"light\"\"\";", line, StringComparison.Ordinal);
    }

    [Fact]
    public void The_point_is_used_even_when_the_machine_uses_a_comma()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-EC");

            var csv = InventoryReportCsv.Build([Row()]);

            Assert.Contains(";10.00;", csv, StringComparison.Ordinal);
            Assert.DoesNotContain("10,00", csv, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    private static InventoryReportRowDto Row() => new()
    {
        ProductId = Guid.NewGuid(),
        Code = "D123",
        Name = "Artículo D",
        Manufacturer = "Cole",
        Description = "Descripción D",
        UnitCost = 10m,
        Stock = 20,
        InventoryValue = 200m,
        ReorderLevel = 50,
        LeadTimeDays = 14,
        LastSaleAt = new DateTime(2026, 11, 1, 15, 30, 0, DateTimeKind.Utc),
        ReorderQuantity = 10,
        Notes = null,
        LastSupplier = "Proveedor X",
        NeedsReorder = true,
    };
}
