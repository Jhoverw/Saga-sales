namespace SagaSales.Sales.Commands;

using SagaSales.Sales.Domain;
using SagaSales.Sales.Infrastructure;

/// <summary>
/// Places an order: reserves stock in Catalog (atomic), snapshots prices, persists the order
/// and publishes the order events. Reservations are released if anything fails afterwards.
/// </summary>
public record CreateOrderCommand(
    IReadOnlyList<OrderItemRequest> Items,
    string? ShippingAddress,
    string? Notes = null) : ICommand<OrderDto>;

public sealed class CreateOrderCommandHandler(
    SalesDbContext dbContext,
    ICatalogClient catalogClient,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<CreateOrderCommand, OrderDto>
{
    private const decimal HighValueThreshold = 1000m;

    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmpty(request.Items, nameof(request.Items));
        Guard.AgainstNullOrEmpty(request.ShippingAddress, nameof(request.ShippingAddress));

        var tenantId = currentUser.RequireTenantId();
        var customerId = currentUser.RequireUserId();

        var reserved = new List<(Guid ProductId, int Quantity)>();
        var lines = new List<OrderItem>();
        decimal subtotal = 0m;

        try
        {
            foreach (var item in request.Items)
            {
                Guard.AgainstNegativeOrZero(item.Quantity, $"Quantity for product {item.ProductId}");

                var product = await catalogClient.GetProductAsync(item.ProductId, cancellationToken);

                await catalogClient.ReserveStockAsync(item.ProductId, item.Quantity, cancellationToken);
                reserved.Add((item.ProductId, item.Quantity));

                subtotal += product.BasePrice * item.Quantity;

                lines.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.BasePrice,
                    ProductSnapshotName = product.Name,
                    ProductSnapshotPrice = product.BasePrice,
                    ProductSnapshotStockAtPurchase = product.StockQuantity,
                });
            }

            var order = new Order
            {
                TenantId = tenantId,
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                Subtotal = subtotal,
                ShippingAddress = request.ShippingAddress!,
                Notes = request.Notes ?? string.Empty,
                OrderDate = DateTime.UtcNow,
                CreatedBy = customerId,
                Items = lines,
            };

            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);

            var snapshot = lines
                .Select(i => new OrderItemSnapshot(i.ProductId, i.ProductSnapshotName, i.Quantity, i.UnitPrice))
                .ToList();

            await eventBus.PublishAsync(
                new OrderPlacedEvent(
                    order.Id,
                    tenantId,
                    customerId,
                    currentUser.Email ?? string.Empty,
                    snapshot,
                    subtotal,
                    order.OrderDate),
                cancellationToken);

            if (subtotal >= HighValueThreshold)
            {
                await eventBus.PublishAsync(
                    new HighValueOrderAlertEvent(order.Id, tenantId, subtotal, order.OrderDate),
                    cancellationToken);
            }

            return Result<OrderDto>.Success(order.ToDto());
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
