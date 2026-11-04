namespace SagaSales.Catalog.Commands;

/// <summary>
/// Atomically reserves stock for an order line. The whole operation is one conditional
/// transaction inside <c>sp_stock_reserve</c>, so concurrent orders cannot oversell (FR-011).
/// </summary>
public record ReserveStockCommand(Guid ProductId, int Quantity) : ICommand<ProductDto>;

public sealed class ReserveStockCommandHandler(
    IProductRepository repository,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<ReserveStockCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.ProductId, nameof(request.ProductId));
        Guard.AgainstNegativeOrZero(request.Quantity, nameof(request.Quantity));

        // NOT_FOUND and INSUFFICIENT_STOCK come from the procedure and map to 404 / 409.
        var reservation = await repository.ReserveStockAsync(request.ProductId, request.Quantity, cancellationToken);

        await eventBus.PublishAsync(
            new CatalogProductUpdatedEvent(
                reservation.Id,
                currentUser.RequireTenantId(),
                reservation.Name,
                reservation.Price,
                reservation.StockQuantity),
            cancellationToken);

        return Result<ProductDto>.Success(new ProductDto
        {
            Id = reservation.Id,
            Name = reservation.Name,
            BasePrice = reservation.Price,
            Currency = reservation.Currency,
            StockQuantity = reservation.StockQuantity,
        });
    }
}
