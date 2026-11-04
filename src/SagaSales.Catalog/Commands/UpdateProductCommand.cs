namespace SagaSales.Catalog.Commands;

/// <summary>
/// Updates a product. FR-001 requires a PUT endpoint; the previous implementation had none.
/// </summary>
public record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid? CategoryId,
    ProductStatus Status) : ICommand<ProductDto>;

public sealed class UpdateProductCommandHandler(
    CatalogDbContext dbContext,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<UpdateProductCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));
        Guard.AgainstNullOrEmpty(request.Name, nameof(request.Name));
        Guard.AgainstNegativeOrZero(request.Price, nameof(request.Price));
        Guard.AgainstNegative(request.StockQuantity, nameof(request.StockQuantity));

        var tenantId = currentUser.RequireTenantId();

        var product = await dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.TenantId == tenantId, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("Product", request.Id);
        }

        product.ApplyDetails(
            request.Name.Trim(),
            request.Description,
            request.Price,
            string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            request.StockQuantity,
            request.CategoryId,
            request.Status);

        await dbContext.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new CatalogProductUpdatedEvent(
                product.Id,
                product.TenantId,
                product.Name,
                product.Price,
                product.StockQuantity),
            cancellationToken);

        return Result<ProductDto>.Success(product.ToDto());
    }
}
