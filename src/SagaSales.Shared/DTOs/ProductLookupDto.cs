namespace SagaSales.Shared.DTOs;

public class ProductLookupDto
{
    public Guid Id { get; set; }
    public string? CodigoBarras { get; set; }
    public string Nombre { get; set; } = default!;
    public string? ThumbnailUrl { get; set; }
    public int Stock { get; set; }
}
