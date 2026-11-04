namespace SagaSales.Shared.DTOs;

public class ProductDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "USD";

    public int StockQuantity { get; set; }

    public Guid? CategoryId { get; set; }

    public Enums.ProductStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public Guid? CategoryId { get; set; }

    public string Currency { get; set; } = "USD";
}

public class UpdateProductRequest : CreateProductRequest
{
}

public class CategoryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }

    public Guid? ParentId { get; set; }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }

    public Guid? ParentId { get; set; }
}
