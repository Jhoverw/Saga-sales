namespace SagaSales.Catalog.Commands;

public record DeleteProductCommand(Guid Id) : ICommand;

public sealed class DeleteProductCommandHandler(
    CatalogDbContext dbContext,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<DeleteProductCommand>
{
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));

        var tenantId = currentUser.RequireTenantId();

        var product = await dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.TenantId == tenantId, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("Product", request.Id);
        }

        // Soft delete: keeps order history and stock figures consistent.
        product.Status = ProductStatus.Discontinued;
        product.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new CatalogProductDeletedEvent(product.Id, product.TenantId),
            cancellationToken);

        return Result.Success();
    }
}
