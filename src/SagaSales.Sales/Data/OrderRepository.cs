namespace SagaSales.Sales.Data;

using System.Text.Json;
using Dapper;
using SagaSales.BuildingBlocks.Data;
using SagaSales.Sales.Domain;

/// <summary>
/// Orders, through the procedures in <c>db/sales/V002__functions.sql</c>.
/// The header and its lines are written in one transaction inside <c>sp_order_create</c>.
/// </summary>
public interface IOrderRepository
{
    Task<Order> CreateAsync(
        Guid customerId,
        string shippingAddress,
        string? notes,
        Guid createdBy,
        IReadOnlyList<OrderItem> lines,
        string? createdByName,
        CancellationToken cancellationToken);

    Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderItem>> GetLinesAsync(Guid orderId, CancellationToken cancellationToken);

    Task<OrderListResult> ListAsync(int pageNumber, int pageSize, OrderStatus? status, CancellationToken cancellationToken);

    Task<Order> ChangeStatusAsync(Guid orderId, OrderStatus newStatus, CancellationToken cancellationToken);

    /// <summary>Pasa a Enviado registrando cómo sale el pedido (spec 007).</summary>
    Task<Order> ShipAsync(Guid orderId, ShipmentRequest shipment, CancellationToken cancellationToken);

    Task<OrderShipmentDto?> GetShipmentAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Order> RegisterPaymentAsync(Guid orderId, decimal amount, CancellationToken cancellationToken);
}

public sealed record OrderListResult(IReadOnlyList<Order> Items, int TotalCount);

public sealed class OrderRepository(
    IStoredProcedureExecutor executor,
    ILogger<OrderRepository> logger) : IOrderRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Order> CreateAsync(
        Guid customerId,
        string shippingAddress,
        string? notes,
        Guid createdBy,
        IReadOnlyList<OrderItem> lines,
        string? createdByName,
        CancellationToken cancellationToken)
    {
        // The lines travel as JSON so header and lines are inserted in one transaction.
        var payload = JsonSerializer.Serialize(
            lines.Select(l => new
            {
                productId = l.ProductId,
                productName = l.ProductSnapshotName,
                quantity = l.Quantity,
                unitPrice = l.UnitPrice,
                priceSource = (short)PriceSource.Base,
                stockAtPurchase = l.ProductSnapshotStockAtPurchase,
                isTaxable = l.IsTaxable,
                priceIncludesTax = l.PriceIncludesTax,
            }),
            Json);

        var arguments = SqlArgs.New()
            .With("customer_id", customerId)
            .With("shipping_address", shippingAddress)
            .With("notes", notes)
            .With("created_by", createdBy)
            .With("created_by_name", string.IsNullOrWhiteSpace(createdByName) ? null : createdByName.Trim())
            .With("lines_json", payload);

        var row = await executor.QueryProcSingleAsync<OrderRow>(
            "dbo.sp_order_create", arguments, cancellationToken: cancellationToken);

        logger.LogInformation("Order {OrderId} created with {Lines} line(s)", row!.Id, lines.Count);
        return row.ToOrder(lines.ToList());
    }

    public async Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("order_id", orderId);

        var row = await executor.QueryProcSingleAsync<OrderRow>(
            "dbo.sp_order_get", arguments, cancellationToken: cancellationToken);

        return row?.ToOrder([]);
    }

    public async Task<IReadOnlyList<OrderItem>> GetLinesAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("order_id", orderId);

        var rows = await executor.QueryProcAsync<OrderLineRow>(
            "dbo.sp_order_lines", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToItem()).ToList();
    }

    public async Task<OrderListResult> ListAsync(
        int pageNumber,
        int pageSize,
        OrderStatus? status,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("page_number", pageNumber)
            .With("page_size", pageSize)
            .With("status", status is null ? null : (short)status.Value);

        var rows = await executor.QueryProcAsync<OrderRow>(
            "dbo.sp_order_list", arguments, cancellationToken: cancellationToken);

        var orders = rows.Select(r => r.ToOrder([])).ToList();
        var total = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new OrderListResult(orders, total);
    }

    public async Task<Order> ChangeStatusAsync(Guid orderId, OrderStatus newStatus, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("order_id", orderId)
            .With("new_status", (short)newStatus);

        // INVALID_TRANSITION comes from the procedure and maps to 409.
        var row = await executor.QueryProcSingleAsync<OrderRow>(
            "dbo.sp_order_change_status", arguments, cancellationToken: cancellationToken);

        return row!.ToOrder([]);
    }

    public async Task<Order> ShipAsync(Guid orderId, ShipmentRequest shipment, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("order_id", orderId)
            .With("shipping_method", (short)shipment.ShippingMethod)
            .With("courier_company", shipment.CourierCompany)
            .With("tracking_number", shipment.TrackingNumber);

        // VALIDATION_ERROR (falta empresa o guía) e INVALID_TRANSITION vienen del procedure.
        var row = await executor.QueryProcSingleAsync<OrderRow>(
            "dbo.sp_order_ship", arguments, cancellationToken: cancellationToken);

        return row!.ToOrder([]);
    }

    public async Task<OrderShipmentDto?> GetShipmentAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var row = await executor.QueryProcSingleAsync<ShipmentRow>(
            "dbo.sp_order_shipment_get",
            SqlArgs.New().With("order_id", orderId),
            cancellationToken: cancellationToken);

        return row is null
            ? null
            : new OrderShipmentDto
            {
                ShippingMethod = row.ShippingMethod,
                CourierCompany = row.CourierCompany,
                TrackingNumber = row.TrackingNumber,
                ShippedAt = row.ShippedAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null,
            };
    }

    private sealed class ShipmentRow
    {
        public short? ShippingMethod { get; set; }

        public string? CourierCompany { get; set; }

        public string? TrackingNumber { get; set; }

        public DateTime? ShippedAt { get; set; }
    }

    public async Task<Order> RegisterPaymentAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("order_id", orderId)
            .With("amount", amount);

        var row = await executor.QueryProcSingleAsync<OrderRow>(
            "dbo.sp_order_register_payment", arguments, cancellationToken: cancellationToken);

        return row!.ToOrder([]);
    }

    private sealed class OrderRow
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public Guid CustomerId { get; set; }

        public short Status { get; set; }

        public decimal Subtotal { get; set; }

        // Nullable on purpose: orders created before V-03 have no tax_amount (NULL in SQL).
        public decimal? TaxAmount { get; set; }

        public string? OrderNumber { get; set; }

        public decimal PaidAmount { get; set; }

        public string ShippingAddress { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public DateTime OrderDate { get; set; }

        public DateTime? PaidDate { get; set; }

        public Guid CreatedBy { get; set; }

        public string? CreatedByName { get; set; }

        public int TotalCount { get; set; }

        public Order ToOrder(List<OrderItem> items) => new()
        {
            Id = Id,
            TenantId = TenantId,
            CustomerId = CustomerId,
            Status = (OrderStatus)Status,
            Subtotal = Subtotal,
            TaxAmount = TaxAmount ?? 0m,
            OrderNumber = OrderNumber ?? string.Empty,
            PaidAmount = PaidAmount,
            ShippingAddress = ShippingAddress,
            Notes = Notes ?? string.Empty,
            OrderDate = OrderDate,
            PaidDate = PaidDate,
            CreatedBy = CreatedBy,
            CreatedByName = CreatedByName ?? string.Empty,
            Items = items,
        };
    }

    private sealed class OrderLineRow
    {
        public Guid ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public short PriceSource { get; set; }

        public bool IsTaxable { get; set; } = true;

        public bool PriceIncludesTax { get; set; }

        public decimal UnitPriceWithTax { get; set; }

        public decimal LineNet { get; set; }

        public decimal LineTax { get; set; }

        public decimal LineTotal { get; set; }

        public OrderItem ToItem() => new()
        {
            ProductId = ProductId,
            ProductSnapshotName = ProductName,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            ProductSnapshotPrice = UnitPrice,
            IsTaxable = IsTaxable,
            PriceIncludesTax = PriceIncludesTax,
            UnitPriceWithTax = UnitPriceWithTax,
            LineNet = LineNet,
            LineTax = LineTax,
            LineTotal = LineTotal,
        };
    }
}
