namespace SagaSales.Payment.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;

[ApiController]
[Authorize]
[Route("api/payments")]
public sealed class PaymentsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Records a charge received outside the platform (transfer, cash, deposit) and updates the
    /// order. A second live payment for the same order returns 409.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] ManualPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.CreatePaymentCommand(
            request.OrderId,
            MapMethod(request.Method),
            request.Amount,
            request.Reference,
            request.PaidAt,
            CashReceived: request.CashReceived);

        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetPaymentQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByOrder(
        [FromQuery] Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetPaymentsByOrderQuery(orderId), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Refunds all or part of a charge. Past what was charged returns 409.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPost("{id:guid}/refund")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RefundPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new Commands.RefundPaymentCommand(id, request.RefundAmount),
            cancellationToken);

        return Ok(result.Value);
    }

    // The manual methods map onto the general payment method, which the order and the reports use.
    private static PaymentMethod MapMethod(ManualPaymentMethod method) => method switch
    {
        ManualPaymentMethod.Card => PaymentMethod.CreditCard,
        ManualPaymentMethod.Cash => PaymentMethod.CashOnDelivery,
        ManualPaymentMethod.Deposit => PaymentMethod.BankTransfer,
        _ => PaymentMethod.BankTransfer,
    };
}
