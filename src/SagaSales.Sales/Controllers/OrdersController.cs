namespace SagaSales.Sales.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        // D8: staff places the order for a customer, so the customer comes from the body
        // and the signed-in user is recorded as `created_by`.
        var command = new Commands.CreateOrderCommand(
            request.CustomerId,
            request.Items.Select(i => new OrderItemRequest { ProductId = i.ProductId, Quantity = i.Quantity }).ToList(),
            request.ShippingAddress,
            request.Notes,
            request.Channel,
            request.CashOnDelivery);

        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] OrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetOrdersQuery(pageNumber, pageSize, status), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Panel "Mi día" (spec 006): ventas por día local de los últimos <paramref name="days"/> días y
    /// productos vendidos hoy. El cajero solo recibe lo suyo; dueño y administrador pueden filtrar.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
    [HttpGet("seller-summary")]
    [ProducesResponseType(typeof(SellerSalesSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerSummary(
        [FromQuery] int days = 15,
        [FromQuery] int offsetMinutes = 0,
        [FromQuery] Guid? sellerId = null,
        CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > SagaSales.Shared.Time.LocalDayRange.MaxDays || offsetMinutes is < -840 or > 840)
        {
            return BadRequest(new { message = "Rango de días o desfase horario fuera de rango." });
        }

        var range = SagaSales.Shared.Time.LocalDayRange.LastDays(days, offsetMinutes, DateTime.UtcNow);
        var onlyCaller = !User.IsInRole(SagaSalesRoles.Owner) && !User.IsInRole(SagaSalesRoles.Manager);

        var result = await mediator.Send(new Queries.GetSellerSummaryQuery(range, sellerId, onlyCaller), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetOrderQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Cómo salió el pedido: método, courier, guía y fecha (spec 007).</summary>
    [HttpGet("{id:guid}/shipment")]
    [ProducesResponseType(typeof(OrderShipmentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShipment(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetOrderShipmentQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    // Spec 008: the seller who runs the shipments moves orders along; cancelling stays with managers.
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        // A new command is built from the route id: records are immutable.
        var command = new Commands.UpdateOrderStatusCommand(id, (OrderStatus)request.NewStatus, request.Shipment);
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Records money received for this order. Called by the Payment service after a charge exists,
    /// so the order's paid amount and status follow the money.
    /// </summary>
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegisterPayment(
        Guid id,
        [FromBody] RegisterOrderPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new Commands.RegisterOrderPaymentCommand(id, request.Amount, request.Verified),
            cancellationToken);

        return Ok(result.Value);
    }

    // ── spec 008: pedidos ─────────────────────────────────────────────────────

    /// <summary>Payment confirmed that money it had recorded really arrived.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
    [HttpPost("{id:guid}/payments/verified")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PaymentVerified(Guid id, [FromBody] OrderPaymentAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.OrderPaymentVerifiedCommand(id, request.Amount), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Payment rejected money that never arrived: it stops counting as paid.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
    [HttpPost("{id:guid}/payments/rejected")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PaymentRejected(Guid id, [FromBody] OrderPaymentAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.OrderPaymentRejectedCommand(id, request.Amount), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Canal, apartado, verificación y envío del pedido.</summary>
    [HttpGet("{id:guid}/fulfillment")]
    [ProducesResponseType(typeof(OrderFulfillmentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFulfillment(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetOrderFulfillmentQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Pedidos en curso para el tablero, cada uno con su columna.</summary>
    [HttpGet("remote")]
    [ProducesResponseType(typeof(IReadOnlyList<RemoteOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRemote(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetRemoteOrdersQuery(), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Unidades apartadas por pedidos que aún no salen, por producto (siguen en la bodega).</summary>
    [HttpGet("reserved-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<ReservedStockDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReservedStock(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetReservedStockQuery(), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Qué pedidos apartan un producto.</summary>
    [HttpGet("reserved-stock/{productId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ReservedOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReservedStockDetail(Guid productId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetReservedStockDetailQuery(productId), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(SalesSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetSalesSettingsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Owner)]
    [HttpPut("settings")]
    [ProducesResponseType(typeof(SalesSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetSettings([FromBody] SalesSettingsDto request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.SetSalesSettingsCommand(request.OrderHoldHours), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Cancels the order and returns its stock to the branch it came from.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.CancelOrderCommand(id), cancellationToken);
        return Ok(result.Value);
    }
}
