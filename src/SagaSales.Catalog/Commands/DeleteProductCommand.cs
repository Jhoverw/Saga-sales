namespace SagaSales.Catalog.Commands;

/// <summary>
/// Retires a product. The row is never deleted, so historical orders keep their name and price
/// (FR-010); the procedure also raises NOT_FOUND when the product does not belong to the tenant.
/// </summary>
public record DeleteProductCommand(Guid Id) : ICommand;

public sealed class DeleteProductCommandHandler(
    IProductRepository repository,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<DeleteProductCommand>
{
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));

        await repository.RetireAsync(request.Id, cancellationToken);

        await eventBus.PublishAsync(
            new CatalogProductDeletedEvent(request.Id, currentUser.RequireTenantId()),
            cancellationToken);

        return Result.Success();
    }
}
