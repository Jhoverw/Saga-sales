// Product Endpoints — Catalog Service Contract
// This file defines the REST API contract for product operations.
// Implement the endpoints per these specifications.

namespace SagaSales.Contracts;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public int StockQuantity { get; set; }
    public Guid? CategoryId { get; set; }
    public ProductStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
    public Guid? ParentId { get; set; }
}

public class CreateProductRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; } = 0;
    public Guid? CategoryId { get; set; }
}

public class UpdateProductRequest : CreateProductRequest { }

public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

// HTTP Endpoints
// POST   /api/products          → CreateProduct (returns 201 ProductDto)
// GET    /api/products          → ListProducts (paged, filtered by category, sorted)
// GET    /api/products/{id}     → GetProduct by ID
// PUT    /api/products/{id}     → UpdateProduct (returns 200 ProductDto)
// DELETE /api/products/{id}     → DeleteProduct (returns 204, no body)

// Domain Events Published
// CatalogProductCreatedEvent  ← on successful create
// CatalogProductUpdatedEvent ← on successful update
// CatalogProductDeletedEvent  ← on successful delete
