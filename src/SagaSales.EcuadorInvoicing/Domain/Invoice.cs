namespace SagaSales.EcuadorInvoicing.Domain;

public class Invoice
{
    public Guid Id { get; set; }
    public string ClaveAcceso { get; set; } = default!; // 49‑digit access key
    public string RucEmisor { get; set; } = default!;
    public string RazonSocialEmisor { get; set; } = default!;
    public string DireccionEmisor { get; set; } = default!;
    public string Establecimiento { get; set; } = default!;
    public string PuntoEmision { get; set; } = default!;
    public string RucComprador { get; set; } = default!;
    public string? RazonSocialComprador { get; set; }
    public string? DireccionComprador { get; set; }
    public DateTime FechaEmision { get; set; }
    public DateTime? FechaAutorizacion { get; set; }
    public string Ambiente { get; set; } = default!; // 1=Pruebas, 2=Producción
    public string Serie { get; set; } = default!; // 3 dígitos
    public int Numero { get; set; }
    public List<InvoiceItem> Items { get; set; } = new();
    public decimal TotalSinImpuestos { get; set; }
    public decimal TotalImpuestos { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = "PENDIENTE"; // PENDIENTE, AUTORIZADO, RECHAZADO
    public string? MensajeRespuestaSRI { get; set; }
}
