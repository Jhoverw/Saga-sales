namespace SagaSales.Catalog.Services;

using System.Globalization;
using System.Text;
using SagaSales.Shared.DTOs;

/// <summary>
/// The inventory control report as CSV (spec 004, FR-009): ';' between columns, decimal point
/// (constitution v2.4.0), dates as yyyy-MM-dd, text quoted when it contains a separator. The UTF-8
/// BOM is added by the endpoint so Excel opens it without an import wizard.
/// </summary>
public static class InventoryReportCsv
{
    private static readonly string[] Header =
    [
        "Reponer", "N.º de artículo", "Nombre", "Fabricante", "Descripción", "Costo por artículo",
        "Cantidad de existencias", "Valor del inventario", "Nivel de reposición", "Días por reposición",
        "Fecha de última venta", "Cantidad de reposición", "Notas", "Último proveedor",
    ];

    public static string Build(IReadOnlyList<InventoryReportRowDto> rows)
    {
        var csv = new StringBuilder();
        csv.Append(string.Join(';', Header)).Append("\r\n");

        foreach (var row in rows)
        {
            string[] cells =
            [
                row.NeedsReorder ? "REPONER" : "OK",
                Text(row.Code),
                Text(row.Name),
                Text(row.Manufacturer),
                Text(row.Description),
                Money(row.UnitCost),
                row.Stock.ToString(CultureInfo.InvariantCulture),
                Money(row.InventoryValue),
                Whole(row.ReorderLevel),
                Whole(row.LeadTimeDays),
                row.LastSaleAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                Whole(row.ReorderQuantity),
                Text(row.Notes),
                Text(row.LastSupplier),
            ];

            csv.Append(string.Join(';', cells)).Append("\r\n");
        }

        return csv.ToString();
    }

    private static string Money(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Whole(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.IndexOfAny([';', '"', '\r', '\n']) >= 0;
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
