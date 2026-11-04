namespace SagaSales.Sales.Mapping;

using SagaSales.Sales.Domain;

public static class OrderMapping
{
    public static OrderDto ToDto(this Order order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        Status = order.Status.ToString(),
        Subtotal = order.Subtotal,
        PaidAmount = order.PaidAmount,
        ShippingAddress = order.ShippingAddress,
        OrderDate = order.OrderDate,
        PaidDate = order.PaidDate,
        Items = order.Items.Select(i => i.ToDto()).ToList(),
    };

    public static OrderItemDto ToDto(this OrderItem item) => new()
    {
        ProductId = item.ProductId,
        ProductName = item.ProductSnapshotName,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice,
    };
}
