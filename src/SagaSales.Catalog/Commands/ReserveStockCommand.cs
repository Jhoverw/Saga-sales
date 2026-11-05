namespace SagaSales.Catalog.Commands;

/// <summary>
/// Atomically reserves stock in one branch (E-13, D16). When the branch is not given, the store's
/// default branch is used. For products that track lots, the oldest expiry is consumed first (FEFO)
/// and an expired lot is never sold (D18).
/// </summary>
public record ReserveStockCommand(Guid ProductId, int Quantity, Guid? BranchId = null)
    : ICommand<ProductDto>;

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
        var reservation = await repository.ReserveStockAsync(
            request.ProductId, request.Quantity, request.BranchId, cancellationToken);

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
