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

    // ── spec 008: pedidos ────────────────────────────────────────────────────
    Task<Order> RegisterPaymentAsync(Guid orderId, decimal amount, bool verified, CancellationToken cancellationToken);

    Task SetChannelAsync(Guid orderId, int channel, bool cashOnDelivery, CancellationToken cancellationToken);

    Task<Order> PaymentVerifiedAsync(Guid orderId, decimal amount, CancellationToken cancellationToken);

    Task<Order> PaymentRejectedAsync(Guid orderId, decimal amount, CancellationToken cancellationToken);

    Task<OrderFulfillmentDto?> GetFulfillmentAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RemoteOrderDto>> ListRemoteAsync(CancellationToken cancellationToken);

    Task<SalesSettingsDto> GetSettingsAsync(CancellationToken cancellationToken);

    Task<SalesSettingsDto> SetSettingsAsync(int orderHoldHours, CancellationToken cancellationToken);

    /// <summary>Pedidos vencidos de todas las tiendas (sin tienda: lo usa el servicio de vencimiento).</summary>
    Task<IReadOnlyList<(Guid TenantId, Guid OrderId)>> HoldsDueAsync(int maxRows, CancellationToken cancellationToken);

    Task MarkExpiredAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReservedStockDto>> ReservedStockAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ReservedOrderDto>> ReservedStockDetailAsync(Guid productId, CancellationToken cancellationToken);
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

    public Task<Order> RegisterPaymentAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
        => RegisterPaymentAsync(orderId, amount, verified: true, cancellationToken);

    public async Task<Order> RegisterPaymentAsync(Guid orderId, decimal amount, bool verified, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("order_id", orderId)
            .With("amount", amount)
            .With("verified", verified);

        var row = await executor.QueryProcSingleAsync<OrderRow>(
            "dbo.sp_order_register_payment", arguments, cancellationToken: cancellationToken);

        return row!.ToOrder([]);
    }

    public Task SetChannelAsync(Guid orderId, int channel, bool cashOnDelivery, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("order_id", orderId)
            .With("channel", (short)channel)
            .With("cash_on_delivery", cashOnDelivery);

        return executor.ExecuteProcAsync("dbo.sp_order_set_channel", arguments, cancellationToken: cancellationToken);
    }

    public Task<Order> PaymentVerifiedAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
        => AdjustAsync("dbo.sp_order_payment_verified", orderId, amount, cancellationToken);

    public Task<Order> PaymentRejectedAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
        => AdjustAsync("dbo.sp_order_payment_rejected", orderId, amount, cancellationToken);

    public async Task<OrderFulfillmentDto?> GetFulfillmentAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var row = await executor.QueryProcSingleAsync<FulfillmentRow>(
            "dbo.sp_order_fulfillment_get",
            SqlArgs.New().With("order_id", orderId),
            cancellationToken: cancellationToken);

        return row?.ToDto();
    }

    public async Task<IReadOnlyList<RemoteOrderDto>> ListRemoteAsync(CancellationToken cancellationToken)
    {
        var rows = await executor.QueryProcAsync<RemoteRow>(
            "dbo.sp_order_remote_list", SqlArgs.New(), cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<SalesSettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
        => (await executor.QueryProcSingleAsync<SalesSettingsDto>(
               "dbo.sp_sales_settings_get", SqlArgs.New(), cancellationToken: cancellationToken))!;

    public async Task<SalesSettingsDto> SetSettingsAsync(int orderHoldHours, CancellationToken cancellationToken)
        => (await executor.QueryProcSingleAsync<SalesSettingsDto>(
               "dbo.sp_sales_settings_set",
               SqlArgs.New().With("order_hold_hours", orderHoldHours),
               cancellationToken: cancellationToken))!;

    public async Task<IReadOnlyList<(Guid TenantId, Guid OrderId)>> HoldsDueAsync(int maxRows, CancellationToken cancellationToken)
    {
        // The one call without a tenant: it lists every store's expired holds; each is then cancelled in its own store.
        var rows = await executor.QueryProcAsync<HoldRow>(
            "dbo.sp_order_holds_due",
            SqlArgs.New().With("max_rows", maxRows),
            tenantScoped: false,
            cancellationToken: cancellationToken);

        return rows.Select(r => (r.TenantId, r.OrderId)).ToList();
    }

    public Task MarkExpiredAsync(Guid orderId, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_order_mark_expired", SqlArgs.New().With("order_id", orderId), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<ReservedStockDto>> ReservedStockAsync(CancellationToken cancellationToken)
        => (await executor.QueryProcAsync<ReservedStockDto>(
               "dbo.sp_order_reserved_stock", SqlArgs.New(), cancellationToken: cancellationToken)).ToList();

    public async Task<IReadOnlyList<ReservedOrderDto>> ReservedStockDetailAsync(Guid productId, CancellationToken cancellationToken)
    {
        var rows = await executor.QueryProcAsync<ReservedRow>(
            "dbo.sp_order_reserved_stock_detail",
            SqlArgs.New().With("product_id", productId),
            cancellationToken: cancellationToken);

        return rows.Select(r => new ReservedOrderDto
        {
            OrderId = r.OrderId,
            OrderNumber = r.OrderNumber,
            CustomerId = r.CustomerId,
            Status = ((OrderStatus)r.Status).ToString(),
            Quantity = r.Quantity,
            HoldUntil = Utc(r.HoldUntil),
            CashOnDelivery = r.CashOnDelivery,
            OrderDate = DateTime.SpecifyKind(r.OrderDate, DateTimeKind.Utc),
            VerifiedAmount = r.VerifiedAmount,
            Total = r.Total,
        }).ToList();
    }

    private sealed class ReservedRow
    {
        public Guid OrderId { get; set; }

        public string? OrderNumber { get; set; }

        public Guid CustomerId { get; set; }

        public short Status { get; set; }

        public int Quantity { get; set; }

        public DateTime? HoldUntil { get; set; }

        public bool CashOnDelivery { get; set; }

        public DateTime OrderDate { get; set; }

        public decimal VerifiedAmount { get; set; }

        public decimal Total { get; set; }
    }

    private async Task<Order> AdjustAsync(string procedure, Guid orderId, decimal amount, CancellationToken cancellationToken)
    {
        var row = await executor.QueryProcSingleAsync<OrderRow>(
            procedure,
            SqlArgs.New().With("order_id", orderId).With("amount", amount),
            cancellationToken: cancellationToken);

        return row!.ToOrder([]);
    }

    private sealed class HoldRow
    {
        public Guid TenantId { get; set; }

        public Guid OrderId { get; set; }
    }

    private sealed class FulfillmentRow
    {
        public short Channel { get; set; }

        public bool CashOnDelivery { get; set; }

        public DateTime? HoldUntil { get; set; }

        public DateTime? ExpiredAt { get; set; }

        public decimal VerifiedAmount { get; set; }

        public decimal PaidAmount { get; set; }

        public decimal Total { get; set; }

        public short? ShippingMethod { get; set; }

        public string? CourierCompany { get; set; }

        public string? TrackingNumber { get; set; }

        public DateTime? ShippedAt { get; set; }

        public OrderFulfillmentDto ToDto() => new()
        {
            Channel = Channel,
            CashOnDelivery = CashOnDelivery,
            HoldUntil = Utc(HoldUntil),
            ExpiredAt = Utc(ExpiredAt),
            VerifiedAmount = VerifiedAmount,
            PaidAmount = PaidAmount,
            Total = Total,
            ShippingMethod = ShippingMethod,
            CourierCompany = CourierCompany,
            TrackingNumber = TrackingNumber,
            ShippedAt = Utc(ShippedAt),
        };
    }

    private sealed class RemoteRow
    {
        public Guid Id { get; set; }

        public string? OrderNumber { get; set; }

        public Guid CustomerId { get; set; }

        public short Status { get; set; }

        public decimal Total { get; set; }

        public decimal PaidAmount { get; set; }

        public decimal VerifiedAmount { get; set; }

        public bool CashOnDelivery { get; set; }

        public DateTime? HoldUntil { get; set; }

        public DateTime? ExpiredAt { get; set; }

        public DateTime OrderDate { get; set; }

        public string? CreatedByName { get; set; }

        public RemoteOrderDto ToDto() => new()
        {
            Id = Id,
            OrderNumber = OrderNumber,
            CustomerId = CustomerId,
            Status = ((OrderStatus)Status).ToString(),
            Total = Total,
            PaidAmount = PaidAmount,
            VerifiedAmount = VerifiedAmount,
            CashOnDelivery = CashOnDelivery,
            HoldUntil = Utc(HoldUntil),
            ExpiredAt = Utc(ExpiredAt),
            OrderDate = DateTime.SpecifyKind(OrderDate, DateTimeKind.Utc),
            CreatedByName = CreatedByName,
        };
    }

    private static DateTime? Utc(DateTime? value) => value is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;

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
