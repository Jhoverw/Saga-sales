namespace SagaSales.Payment.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.CreatePaymentCommand(request.OrderId, request.Method, request.GatewayReference);
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("{id:guid}/refund")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RefundPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.RefundPaymentCommand(id, request.RefundAmount);
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetPaymentQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ByOrder([FromQuery] Guid orderId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetPaymentsByOrderQuery(orderId), cancellationToken);
        return Ok(result.Value);
    }
}
