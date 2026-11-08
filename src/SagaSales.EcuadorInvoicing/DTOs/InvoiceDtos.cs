namespace SagaSales.EcuadorInvoicing.DTOs;

public class CreateInvoiceDto
{
    public string RucComprador { get; set; } = default!;
    public string? RazonSocialComprador { get; set; }
    public string? DireccionComprador { get; set; }
    public List<InvoiceItemDto> Items { get; set; } = new();
    public string Ambiente { get; set; } = "1"; // default pruebas
    public string Serie { get; set; } = default!; // 3 digits
    public int Numero { get; set; }
}

public class InvoiceItemDto
{
    public string CodigoInterno { get; set; } = default!;
    public string? CodigoBarras { get; set; }
    public string Descripcion { get; set; } = default!;
    public decimal Cantidad { get; set; }
    public string UnidadMedida { get; set; } = default!;
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public string TipoProducto { get; set; } = "BIEN";
    public string CodigoImpuesto { get; set; } = default!;
}

public class InvoiceResponseDto
{
    public Guid Id { get; set; }
    public string ClaveAcceso { get; set; } = default!;
    public string Estado { get; set; } = default!;
    public string? MensajeRespuestaSRI { get; set; }
    public string UrlVerificacion { get; set; } = default!;
}
