// Catalog Service — REST contract.
// Declarative contract: this file documents the API, it is not compiled.
// Runtime DTOs live in src/SagaSales.Shared/DTOs; procedures in data-model.md §4.

namespace SagaSales.Contracts;

public enum CustomerType
{
    Retail = 1,
    Wholesale = 2,
}

/// <summary>Which price was applied when quoting a product for a buyer (FR-007b).</summary>
public enum PriceSource
{
    Base = 1,
    List = 2,
}

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int StockQuantity { get; set; }
    public Guid? CategoryId { get; set; }
    public ProductStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Resolved price for one product and one customer type.</summary>
public class PriceQuoteDto
{
    public Guid ProductId { get; set; }
    public CustomerType CustomerType { get; set; }
    public decimal Price { get; set; }
    public PriceSource Source { get; set; }
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
    public int StockQuantity { get; set; }
    public Guid? CategoryId { get; set; }
    public string Currency { get; set; } = "USD";
}

public class UpdateProductRequest : CreateProductRequest
{
}

public class SetPriceRequest
{
    public decimal Price { get; set; }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
    public Guid? ParentId { get; set; }
}

/// <summary>Body of the internal stock endpoints used by the Sales service.</summary>
public class StockMovementRequest
{
    public int Quantity { get; set; }
}

// HTTP Endpoints — all require a bearer token and a resolvable tenant (FR-004).
//
// POST   /api/products                              → 201 ProductDto
// GET    /api/products                              → 200 PagedResult<ProductDto>
//        ?pageNumber&pageSize&categoryId&search&sortBy&descending&customerType
//        Filtering happens before pagination (FR-009); prices are resolved for customerType.
// GET    /api/products/{id}?customerType=           → 200 ProductDto  · 404
// PUT    /api/products/{id}                         → 200 ProductDto  · 400 · 404
// DELETE /api/products/{id}                         → 204 (retires, never deletes — FR-010)
// PUT    /api/products/{id}/prices/{customerType}   → 200 PriceQuoteDto (upsert, FR-007)
// GET    /api/products/{id}/price?customerType=     → 200 PriceQuoteDto (fallback reported, FR-007b)
// POST   /api/products/{id}/stock/reserve           → 200 ProductDto · 409 INSUFFICIENT_STOCK
// POST   /api/products/{id}/stock/release           → 204 (compensation, FR-014)
// GET    /api/categories                            → 200 CategoryDto[]
// POST   /api/categories                            → 201 CategoryDto · 409 duplicate name
//
// The two stock endpoints are service-to-service: Sales calls them with the caller's token
// forwarded. They are the only write path to stock, so a client cannot bypass the reservation.

// Errors: 400 VALIDATION_ERROR · 401 · 403 · 404 NOT_FOUND · 409 CONFLICT | INSUFFICIENT_STOCK

// Domain events published
//   CatalogProductCreatedEvent · CatalogProductUpdatedEvent · CatalogProductDeletedEvent
