namespace SagaSales.EcuadorInvoicing.Domain;

public class InvoiceItem
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public string CodigoInterno { get; set; } = default!; // internal product code
    public string? CodigoBarras { get; set; }
    public string Descripcion { get; set; } = default!;
    public decimal Cantidad { get; set; }
    public string UnidadMedida { get; set; } = default!;
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public string TipoProducto { get; set; } = "BIEN"; // BIEN, SERVICIO, MIXTO
    public string CodigoImpuesto { get; set; } = default!; // e.g., 2 (IVA 12%)
    public decimal BaseImponible { get; set; }
    public decimal MontoImpuesto { get; set; }
}
