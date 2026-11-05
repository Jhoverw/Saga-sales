namespace SagaSales.Catalog.Commands;

/// <summary>Updates a product (FR-001).</summary>
public record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid? CategoryId,
    ProductStatus Status,
    bool TracksBatches = false) : ICommand<ProductDto>;

public sealed class UpdateProductCommandHandler(
    IProductRepository repository,
    IEventBus eventBus) : ICommandHandler<UpdateProductCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));
        Guard.AgainstNullOrEmpty(request.Name, nameof(request.Name));
        Guard.AgainstNegativeOrZero(request.Price, nameof(request.Price));
        Guard.AgainstNegative(request.StockQuantity, nameof(request.StockQuantity));

        // A missing product surfaces from the procedure as NOT_FOUND → 404.
        var product = await repository.UpdateAsync(
            request.Id,
            request.Name.Trim(),
            request.Description,
            request.Price,
            string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            request.StockQuantity,
            request.CategoryId,
            request.Status,
            request.TracksBatches,
            cancellationToken);

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
