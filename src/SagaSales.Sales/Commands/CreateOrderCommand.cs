namespace SagaSales.Sales.Commands;

using SagaSales.Sales.Domain;
using SagaSales.Sales.Infrastructure;

/// <summary>
/// Places an order on behalf of a customer (D8): reserves stock in Catalog, snapshots the name and
/// price of every line, persists the order and publishes the events.
/// Reservations already taken are released if anything fails afterwards (FR-014).
/// </summary>
public record CreateOrderCommand(
    Guid CustomerId,
    IReadOnlyList<OrderItemRequest> Items,
    string ShippingAddress,
    string? Notes = null) : ICommand<OrderDto>;

public sealed class CreateOrderCommandHandler(
    IOrderRepository repository,
    ICatalogClient catalogClient,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<CreateOrderCommand, OrderDto>
{
    private const decimal HighValueThreshold = 1000m;

    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.CustomerId, nameof(request.CustomerId));
        Guard.AgainstEmpty(request.Items, nameof(request.Items));
        Guard.AgainstNullOrEmpty(request.ShippingAddress, nameof(request.ShippingAddress));

        var tenantId = currentUser.RequireTenantId();
        var staffUserId = currentUser.RequireUserId();

        var reserved = new List<(Guid ProductId, int Quantity)>();
        var lines = new List<OrderItem>();

        try
        {
            foreach (var item in request.Items)
            {
                Guard.AgainstNegativeOrZero(item.Quantity, $"Quantity for product {item.ProductId}");

                // Reserve first: the catalogue owns stock, and this call is atomic there (FR-011).
                await catalogClient.ReserveStockAsync(item.ProductId, item.Quantity, cancellationToken);
                reserved.Add((item.ProductId, item.Quantity));

                var product = await catalogClient.GetProductAsync(item.ProductId, cancellationToken);

                lines.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.BasePrice,
                    ProductSnapshotName = product.Name,
                    ProductSnapshotPrice = product.BasePrice,
                    ProductSnapshotStockAtPurchase = product.StockQuantity,
                    IsTaxable = product.IsTaxable,
                    PriceIncludesTax = product.PriceIncludesTax,
                });
            }

            // Header and lines are written in one transaction inside the procedure.
            var order = await repository.CreateAsync(
                request.CustomerId,
                request.ShippingAddress!.Trim(),
                request.Notes,
                staffUserId,
                lines,
                currentUser.DisplayName,
                cancellationToken);

            // The server priced every line (spec 003): read them back so the event and the response carry
            // the stored base, VAT and total instead of recomputing them here.
            var stored = await repository.GetLinesAsync(order.Id, cancellationToken);

            var snapshot = stored
                .Select(i => new OrderItemSnapshot(i.ProductId, i.ProductSnapshotName, i.Quantity, i.UnitPrice, i.LineNet))
                .ToList();

            await eventBus.PublishAsync(
                new OrderPlacedEvent(
                    order.Id,
                    tenantId,
                    order.CustomerId,
                    currentUser.Email ?? string.Empty,
                    snapshot,
                    order.Subtotal,
                    order.OrderDate),
                cancellationToken);

            if (order.Subtotal >= HighValueThreshold)
            {
                await eventBus.PublishAsync(
                    new HighValueOrderAlertEvent(order.Id, tenantId, order.Subtotal, order.OrderDate),
                    cancellationToken);
            }

            return Result<OrderDto>.Success(order.ToDtoWith(stored));
        }
        catch
        {
            // Compensate: give back every reservation already made.
            foreach (var (productId, quantity) in reserved)
            {
                try
                {
                    await catalogClient.ReleaseStockAsync(productId, quantity, CancellationToken.None);
                }
                catch
                {
                    // Best effort: a saga/outbox will own this in Phase 2.
                }
            }

            throw;
        }
    }
}
