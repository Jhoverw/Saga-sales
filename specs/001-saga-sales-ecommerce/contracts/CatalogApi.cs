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

    /// <summary>Weighted average cost from received purchases (E-12); null = unknown.</summary>
    public decimal? Cost { get; set; }

    /// <summary>Base price − cost, or null when the cost is unknown.</summary>
    public decimal? Margin => Cost.HasValue ? BasePrice - Cost.Value : null;

    public decimal? MarginPercent => Cost.HasValue && BasePrice > 0
        ? Math.Round(((BasePrice - Cost.Value) / BasePrice) * 100m, 2)
        : null;

    /// <summary>True when the product carries lots; receiving it then requires a lot code.</summary>
    public bool TracksBatches { get; set; }

    /// <summary>Relative URL path of the picture ("images/products/&lt;id&gt;.&lt;ext&gt;"), or null.</summary>
    public string? ImagePath { get; set; }
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

    /// <summary>Mark it when the product expires and must be received with a lot code.</summary>
    public bool TracksBatches { get; set; }
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

    /// <summary>Branch to reserve from; null uses the store's default branch (E-13).</summary>
    public Guid? BranchId { get; set; }
}

/// <summary>One kardex row (E-12): purchase in, sale out, adjustment or return.</summary>
public class StockMovementDto
{
    public Guid Id { get; set; }
    public short Kind { get; set; }          // 1 purchase in · 2 sale out · 3 adjustment · 4 return in
    public int Quantity { get; set; }        // signed
    public decimal? UnitCost { get; set; }
    public int StockAfter { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime OccurredAt { get; set; }
}

// HTTP Endpoints — all require a bearer token and a resolvable tenant (FR-004).
//
// POST   /api/products                              → 201 ProductDto
// GET    /api/products                              → 200 PagedResult<ProductDto>
//        ?pageNumber&pageSize&categoryId&search&sortBy&descending
//        Filtering happens before pagination (FR-009); rows carry the BASE price.
//        The price for a customer type comes from the quote endpoint below (FR-007b).
// GET    /api/products/{id}                          → 200 ProductDto  · 404
// PUT    /api/products/{id}                         → 200 ProductDto  · 400 · 404
// DELETE /api/products/{id}                         → 204 (retires, never deletes — FR-010)
// PUT    /api/products/{id}/prices/{customerType}   → 200 PriceQuoteDto (upsert, FR-007)
// GET    /api/products/{id}/price?customerType=     → 200 PriceQuoteDto (fallback reported, FR-007b)
// POST   /api/products/{id}/stock/reserve           → 200 ProductDto · 409 INSUFFICIENT_STOCK
//        body: { quantity, branchId? } — per-branch, FEFO, expired lot refused (E-13).
// POST   /api/products/{id}/stock/release           → 204 (compensation, FR-014)
// GET    /api/products/{id}/movements?maxRows=       → 200 StockMovementDto[] (the kardex, E-12)
// POST   /api/products/{id}/image                   → 200 ProductDto (multipart field `file`)
//        JPEG/PNG/WebP/GIF, max 5 MB; stores the path and serves it under /images (E-13).
// GET    /images/products/{file}                     → 200 (public static file, no bearer token)
// GET    /api/categories                            → 200 CategoryDto[]
// POST   /api/categories                            → 201 CategoryDto · 409 duplicate name
//
// The two stock endpoints are service-to-service: Sales calls them with the caller's token
// forwarded. They are the only write path to stock, so a client cannot bypass the reservation.

// Errors: 400 VALIDATION_ERROR · 401 · 403 · 404 NOT_FOUND · 409 CONFLICT | INSUFFICIENT_STOCK

// Domain events published
//   CatalogProductCreatedEvent · CatalogProductUpdatedEvent · CatalogProductDeletedEvent
