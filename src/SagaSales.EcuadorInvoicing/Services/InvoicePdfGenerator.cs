using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SagaSales.EcuadorInvoicing.Domain;
using System.IO;

namespace SagaSales.EcuadorInvoicing.Services;

public class InvoicePdfGenerator
{
    public byte[] GeneratePdf(Invoice invoice)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Header().Text($"Factura Electrónica - Clave: {invoice.ClaveAcceso}").SemiBold().FontSize(14);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Emisor: {invoice.RazonSocialEmisor} ({invoice.RucEmisor})");
                    col.Item().Text($"Comprador: {invoice.RazonSocialComprador ?? "-"} ({invoice.RucComprador})");
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });
                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Descripción");
                            header.Cell().Element(CellStyle).AlignCenter().Text("Cant.");
                            header.Cell().Element(CellStyle).AlignCenter().Text("U.M.");
                            header.Cell().Element(CellStyle).AlignRight().Text("P. Unit.");
                            header.Cell().Element(CellStyle).AlignRight().Text("Total");
                        });
                        foreach (var item in invoice.Items)
                        {
                            table.Cell().Element(CellStyle).Text(item.Descripcion);
                            table.Cell().Element(CellStyle).AlignCenter().Text(item.Cantidad.ToString());
                            table.Cell().Element(CellStyle).AlignCenter().Text(item.UnidadMedida);
                            table.Cell().Element(CellStyle).AlignRight().Text(item.PrecioUnitario.ToString("C", SagaSales.Shared.Formatting.AppCulture.Current));
                            var total = item.BaseImponible;
                            table.Cell().Element(CellStyle).AlignRight().Text(total.ToString("C", SagaSales.Shared.Formatting.AppCulture.Current));
                        }
                        // Totals
                        table.Cell().ColumnSpan(4).Element(CellStyle).AlignRight().Text("Subtotal");
                        table.Cell().Element(CellStyle).AlignRight().Text(invoice.TotalSinImpuestos.ToString("C", SagaSales.Shared.Formatting.AppCulture.Current));
                        table.Cell().ColumnSpan(4).Element(CellStyle).AlignRight().Text("Impuestos");
                        table.Cell().Element(CellStyle).AlignRight().Text(invoice.TotalImpuestos.ToString("C", SagaSales.Shared.Formatting.AppCulture.Current));
                        table.Cell().ColumnSpan(4).Element(CellStyle).AlignRight().Text("Total");
                        table.Cell().Element(CellStyle).AlignRight().Text(invoice.Total.ToString("C", SagaSales.Shared.Formatting.AppCulture.Current));
                    });
                });
                page.Footer().AlignCenter().Text($"Estado: {invoice.Estado}");
            });
        });

        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        return ms.ToArray();
    }

    private static IContainer CellStyle(IContainer container) =>
        container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
}
