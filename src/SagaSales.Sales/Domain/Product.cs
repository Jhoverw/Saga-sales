namespace SagaSales.Sales.Domain;

public class Product
{
    public Guid Id { get; set; }
    public string CodigoInterno { get; set; } = default!;
    public string? CodigoBarras { get; set; }
    public string Nombre { get; set; } = default!;
    public string? ThumbnailUrl { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    // Otros campos existentes…
}
