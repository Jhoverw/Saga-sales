namespace SagaSales.Catalog.Domain;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "USD";

    public int StockQuantity { get; set; }

    public Guid? CategoryId { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Guid CreatedBy { get; set; }

    public Category? Category { get; set; }

    public bool IsAvailable(int quantity) => quantity > 0 && StockQuantity >= quantity;

    public void ApplyDetails(
        string name,
        string? description,
        decimal price,
        string currency,
        int stockQuantity,
        Guid? categoryId,
        ProductStatus status)
    {
        Name = name;
        Description = description;
        Price = price;
        Currency = currency;
        StockQuantity = stockQuantity;
        CategoryId = categoryId;
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}
