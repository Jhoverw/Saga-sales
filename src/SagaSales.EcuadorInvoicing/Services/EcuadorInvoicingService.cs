using MediatR;
using SagaSales.EcuadorInvoicing.Commands;
using SagaSales.EcuadorInvoicing.Domain;
using SagaSales.EcuadorInvoicing.DTOs;
using SagaSales.EcuadorInvoicing.Infrastructure;
using SagaSales.EcuadorInvoicing.Queries;
using SagaSales.EcuadorInvoicing.Utilities;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Security.Cryptography.X509Certificates;

namespace SagaSales.EcuadorInvoicing.Services;

public class EcuadorInvoicingService :
    IRequestHandler<CreateInvoiceCommand, Invoice>,
    IRequestHandler<CancelInvoiceCommand>,
    IRequestHandler<GetInvoiceQuery, Invoice?>
{
    private readonly SriClient _sriClient;
    private readonly X509Certificate2 _certificate;
    // Repositories omitted for brevity – assume an EF Core DbContext injected later

    public EcuadorInvoicingService(SriClient sriClient, X509Certificate2 certificate)
    {
        _sriClient = sriClient;
        _certificate = certificate;
    }

    public async Task<Invoice> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        // Map DTO to domain entity (simplified)
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            RucEmisor = "YOUR_RUC", // TODO: fetch from config
            RazonSocialEmisor = "YOUR_RAZON_SOCIAL",
            DireccionEmisor = "YOUR_ADDRESS",
            Establecimiento = "001",
            PuntoEmision = "001",
            RucComprador = dto.RucComprador,
            RazonSocialComprador = dto.RazonSocialComprador,
            DireccionComprador = dto.DireccionComprador,
            FechaEmision = DateTime.UtcNow,
            Ambiente = dto.Ambiente,
            Serie = dto.Serie,
            Numero = dto.Numero,
            Items = new List<InvoiceItem>()
        };
        foreach (var itemDto in dto.Items)
        {
            var item = new InvoiceItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                CodigoInterno = itemDto.CodigoInterno,
                CodigoBarras = itemDto.CodigoBarras,
                Descripcion = itemDto.Descripcion,
                Cantidad = itemDto.Cantidad,
                UnidadMedida = itemDto.UnidadMedida,
                PrecioUnitario = itemDto.PrecioUnitario,
                Descuento = itemDto.Descuento,
                TipoProducto = itemDto.TipoProducto,
                CodigoImpuesto = itemDto.CodigoImpuesto,
                BaseImponible = itemDto.Cantidad * itemDto.PrecioUnitario - itemDto.Descuento,
                MontoImpuesto = 0 // will be calculated later
            };
            // Simple tax calculation placeholder (IVA 12% for code 2)
            if (item.CodigoImpuesto == "2")
                item.MontoImpuesto = Math.Round(item.BaseImponible * 0.12m, 2);
            invoice.Items.Add(item);
        }
        invoice.TotalSinImpuestos = invoice.Items.Sum(i => i.BaseImponible);
        invoice.TotalImpuestos = invoice.Items.Sum(i => i.MontoImpuesto);
        invoice.Total = invoice.TotalSinImpuestos + invoice.TotalImpuestos;

        // Generate clave de acceso
        var codigoNumerico = new Random().Next(0, 99999999).ToString("D8");
        invoice.ClaveAcceso = ClaveAccesoGenerator.Generate(
            invoice.FechaEmision,
            "01", // factura
            invoice.RucEmisor,
            invoice.Ambiente,
            $"{invoice.Establecimiento}{invoice.PuntoEmision}",
            invoice.Numero,
            codigoNumerico);

        // Build XML (simplified)
        var xml = BuildInvoiceXml(invoice);
        // Sign XML
        var signature = DigitalSignatureHelper.SignXml(xml, _certificate);
        // Append signature element (simplified)
        var signedXml = xml.Replace("</infoTributaria>", $"</infoTributaria><signature>{signature}</signature>");

        // Send to SRI
        var sriResponse = await _sriClient.SendInvoiceAsync(signedXml);
        invoice.Estado = sriResponse.Estado;
        invoice.MensajeRespuestaSRI = sriResponse.Mensaje;
        invoice.FechaAutorizacion = DateTime.UtcNow;
        // Persist invoice (omitted – assume DbContext.SaveChangesAsync())
        return invoice;
    }

    public Task Handle(CancelInvoiceCommand request, CancellationToken cancellationToken)
    {
        // Lookup invoice, verify cancellable, create note of credit, etc. (omitted)
        return Task.CompletedTask;
    }

    public Task<Invoice?> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
    {
        // Retrieve from repository (omitted)
        return Task.FromResult<Invoice?>(null);
    }

    private string BuildInvoiceXml(Invoice invoice)
    {
        var doc = new XDocument(
            new XElement("factura",
                new XElement("infoTributaria",
                    new XElement("ambiente", invoice.Ambiente),
                    new XElement("tipoEmision", "1"),
                    new XElement("razonSocial", invoice.RazonSocialEmisor),
                    new XElement("nombreComercial", invoice.RazonSocialEmisor),
                    new XElement("ruc", invoice.RucEmisor),
                    new XElement("claveAcceso", invoice.ClaveAcceso),
                    new XElement("codDoc", "01"),
                    new XElement("estab", invoice.Establecimiento),
                    new XElement("ptoEmi", invoice.PuntoEmision),
                    new XElement("secuencial", invoice.Numero.ToString("D9")),
                    new XElement("dirMatriz", invoice.DireccionEmisor)
                ),
                new XElement("infoFactura",
                    new XElement("fechaEmision", invoice.FechaEmision.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento", invoice.DireccionEmisor),
                    new XElement("razonSocialComprador", invoice.RazonSocialComprador ?? ""),
                    new XElement("identificacionComprador", invoice.RucComprador),
                    new XElement("totalSinImpuestos", invoice.TotalSinImpuestos),
                    new XElement("totalDescuento", 0),
                    new XElement("totalConImpuestos",
                        new XElement("totalImpuesto",
                            new XElement("codigo", "2"),
                            new XElement("codigoPorcentaje", "2"),
                            new XElement("baseImponible", invoice.TotalSinImpuestos),
                            new XElement("valor", invoice.TotalImpuestos)
                        )
                    ),
                    new XElement("propina", 0),
                    new XElement("importeTotal", invoice.Total)
                ),
                new XElement("detalles",
                    new List<XElement>(invoice.Items.Select(i =>
                        new XElement("detalle",
                            new XElement("codigoPrincipal", i.CodigoInterno),
                            new XElement("codigoAuxiliar", i.CodigoBarras ?? ""),
                            new XElement("descripcion", i.Descripcion),
                            new XElement("cantidad", i.Cantidad),
                            new XElement("precioUnitario", i.PrecioUnitario),
                            new XElement("descuento", i.Descuento),
                            new XElement("precioTotalSinImpuesto", i.BaseImponible),
                            new XElement("impuestos",
                                new XElement("impuesto",
                                    new XElement("codigo", i.CodigoImpuesto),
                                    new XElement("codigoPorcentaje", i.CodigoImpuesto == "2" ? "2" : "0"),
                                    new XElement("tarifa", i.CodigoImpuesto == "2" ? "12" : "0"),
                                    new XElement("baseImponible", i.BaseImponible),
                                    new XElement("valor", i.MontoImpuesto)
                                )
                            )
                        )
                    ))
                )
            )
        );
        return doc.Declaration + doc.ToString();
    }
}
