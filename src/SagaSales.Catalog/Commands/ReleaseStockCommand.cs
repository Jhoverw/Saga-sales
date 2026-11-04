namespace SagaSales.Catalog.Commands;

/// <summary>Compensating operation: returns reserved stock when checkout aborts (FR-014).</summary>
public record ReleaseStockCommand(Guid ProductId, int Quantity) : ICommand;

public sealed class ReleaseStockCommandHandler(IProductRepository repository)
    : ICommandHandler<ReleaseStockCommand>
{
    public async Task<Result> Handle(ReleaseStockCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.ProductId, nameof(request.ProductId));
        Guard.AgainstNegativeOrZero(request.Quantity, nameof(request.Quantity));

        await repository.ReleaseStockAsync(request.ProductId, request.Quantity, cancellationToken);

        return Result.Success();
    }
}
