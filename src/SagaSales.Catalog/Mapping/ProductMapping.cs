namespace SagaSales.Catalog.Mapping;

using SagaSales.Catalog.Domain;

public static class ProductMapping
{
    public static ProductDto ToDto(this Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        BasePrice = product.Price,
        Currency = product.Currency,
        StockQuantity = product.StockQuantity,
        CategoryId = product.CategoryId,
        Status = product.Status,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt,
    };

    public static CategoryDto ToDto(this Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Slug = category.Slug,
        ParentId = category.ParentId,
    };
}
