namespace SagaSales.Shared.DTOs;

public class ProductDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>The price that applies when no list price exists for the buyer's customer type.</summary>
    public decimal BasePrice { get; set; }

    public string Currency { get; set; } = "USD";

    public int StockQuantity { get; set; }

    public Guid? CategoryId { get; set; }

    public Enums.ProductStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>Resolved price for one product and one customer type (FR-007b).</summary>
public class PriceQuoteDto
{
    public Guid ProductId { get; set; }

    public Enums.CustomerType CustomerType { get; set; }

    public decimal Price { get; set; }

    /// <summary>Tells the caller whether the list price or the fallback base price was used.</summary>
    public Enums.PriceSource Source { get; set; }
}

public class SetPriceRequest
{
    public decimal Price { get; set; }
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
