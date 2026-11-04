namespace SagaSales.Catalog.Commands;

/// <summary>
/// Compensating operation: returns reserved stock when the checkout flow aborts.
/// </summary>
public record ReleaseStockCommand(Guid ProductId, int Quantity) : ICommand;

public sealed class ReleaseStockCommandHandler(
    CatalogDbContext dbContext,
    ICurrentUser currentUser) : ICommandHandler<ReleaseStockCommand>
{
    public async Task<Result> Handle(ReleaseStockCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.ProductId, nameof(request.ProductId));
        Guard.AgainstNegativeOrZero(request.Quantity, nameof(request.Quantity));

        var tenantId = currentUser.RequireTenantId();

        await dbContext.Products
            .Where(p => p.Id == request.ProductId && p.TenantId == tenantId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity + request.Quantity)
                    .SetProperty(p => p.UpdatedAt, _ => DateTime.UtcNow),
                cancellationToken);

        return Result.Success();
    }
}
