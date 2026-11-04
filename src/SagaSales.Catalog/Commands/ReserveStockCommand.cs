namespace SagaSales.Catalog.Commands;

/// <summary>
/// Atomically reserves stock for an order line. The whole operation is a single
/// conditional UPDATE, so concurrent orders cannot oversell (FR-002).
/// </summary>
public record ReserveStockCommand(Guid ProductId, int Quantity) : ICommand<ProductDto>;

public sealed class ReserveStockCommandHandler(
    CatalogDbContext dbContext,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<ReserveStockCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.ProductId, nameof(request.ProductId));
        Guard.AgainstNegativeOrZero(request.Quantity, nameof(request.Quantity));

        var tenantId = currentUser.RequireTenantId();

        var affected = await dbContext.Products
            .Where(p =>
                p.Id == request.ProductId &&
                p.TenantId == tenantId &&
                p.Status == ProductStatus.Active &&
                p.StockQuantity >= request.Quantity)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity - request.Quantity)
                    .SetProperty(p => p.UpdatedAt, _ => DateTime.UtcNow),
                cancellationToken);

        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.TenantId == tenantId, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("Product", request.ProductId);
        }

        if (affected == 0)
        {
            throw new InsufficientStockException(product.Name, request.Quantity, product.StockQuantity);
        }

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
