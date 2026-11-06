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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetOrderQuery(id), cancellationToken);
        return Ok(result.Value);
    }

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
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.CancelOrderCommand(id), cancellationToken);
        return Ok(result.Value);
    }
}
