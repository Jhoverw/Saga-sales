namespace SagaSales.Sales.Mapping;

using SagaSales.Sales.Domain;

public static class OrderMapping
{
    public static OrderDto ToDto(this Order order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        CustomerId = order.CustomerId,
        Status = order.Status.ToString(),
        Subtotal = order.Subtotal,
        TaxAmount = order.TaxAmount,
        Total = order.Total,
        PaidAmount = order.PaidAmount,
        ShippingAddress = order.ShippingAddress,
        OrderDate = order.OrderDate,
        PaidDate = order.PaidDate,
        CreatedBy = order.CreatedBy,
        CreatedByName = order.CreatedByName,
        Items = order.Items.Select(i => i.ToDto()).ToList(),
    };

    public static OrderLineDto ToDto(this OrderItem item) => new()
    {
        ProductId = item.ProductId,
        ProductName = item.ProductSnapshotName,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice,

        // Until pricing per customer type reaches the order (phase US2/US3), every line is priced
        // from the product's base price.
        PriceSource = PriceSource.Base,
    };

    /// <summary>
    /// Maps an order whose lines were read separately (the order row cannot carry them).
    /// </summary>
    public static OrderDto ToDtoWith(this Order order, IEnumerable<OrderItem> lines)
    {
        var dto = order.ToDto();
        dto.Items = lines.Select(l => l.ToDto()).ToList();
        return dto;
    }
}
