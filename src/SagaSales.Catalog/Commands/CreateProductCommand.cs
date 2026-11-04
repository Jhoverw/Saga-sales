namespace SagaSales.Catalog.Commands;

/// <summary>
/// Creates a product for the caller's tenant and publishes <c>CatalogProductCreatedEvent</c>.
/// </summary>
public record CreateProductCommand(
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid? CategoryId = null) : ICommand<ProductDto>;

public sealed class CreateProductCommandHandler(
    CatalogDbContext dbContext,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<CreateProductCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Name, nameof(request.Name));
        Guard.AgainstNegativeOrZero(request.Price, nameof(request.Price));
        Guard.AgainstNegative(request.StockQuantity, nameof(request.StockQuantity));

        var tenantId = currentUser.RequireTenantId();

        if (request.CategoryId is { } categoryId)
        {
            var exists = await dbContext.Categories
                .AnyAsync(c => c.Id == categoryId && c.TenantId == tenantId, cancellationToken);

            if (!exists)
            {
                throw new NotFoundException("Category", categoryId);
            }
        }

        var product = new Domain.Product
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Price = request.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            StockQuantity = request.StockQuantity,
            CategoryId = request.CategoryId,
            Status = ProductStatus.Active,
            CreatedBy = currentUser.UserId ?? Guid.Empty,
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new CatalogProductCreatedEvent(
                product.Id,
                product.TenantId,
                product.Name,
                product.Price,
                product.Currency,
                product.StockQuantity),
            cancellationToken);

        return Result<ProductDto>.Success(product.ToDto());
    }
}
