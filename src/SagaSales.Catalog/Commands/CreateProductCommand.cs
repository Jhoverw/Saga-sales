namespace SagaSales.Catalog.Commands;

/// <summary>
/// Creates a product for the caller's tenant and publishes <c>CatalogProductCreatedEvent</c>.
/// Validation and the category check live in <c>sp_product_create</c>.
/// </summary>
public record CreateProductCommand(
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid? CategoryId = null,
    bool TracksBatches = false) : ICommand<ProductDto>;

public sealed class CreateProductCommandHandler(
    IProductRepository repository,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<CreateProductCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Name, nameof(request.Name));
        Guard.AgainstNegativeOrZero(request.Price, nameof(request.Price));
        Guard.AgainstNegative(request.StockQuantity, nameof(request.StockQuantity));

        var product = await repository.CreateAsync(
            request.Name.Trim(),
            request.Description,
            request.Price,
            string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            request.StockQuantity,
            request.CategoryId,
            currentUser.UserId ?? Guid.Empty,
            request.TracksBatches,
            cancellationToken);

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
