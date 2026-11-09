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
            request.Notes);

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

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        // A new command is built from the route id: records are immutable.
        var command = new Commands.UpdateOrderStatusCommand(id, (OrderStatus)request.NewStatus);
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
            new Commands.RegisterOrderPaymentCommand(id, request.Amount),
            cancellationToken);

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
