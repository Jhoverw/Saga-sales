using System;

namespace SagaSales.Sales.Domain;

public class ProductImage
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Url { get; set; } = default!;
    public bool IsThumbnail { get; set; }
}
