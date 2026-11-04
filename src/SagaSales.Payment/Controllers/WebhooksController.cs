namespace SagaSales.Payment.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Payment provider callbacks.
/// NOTE: signature verification is still pending (Phase 2, needs the provider SDK).
/// Until then the endpoint only accepts the provider reference and must not be exposed
/// publicly.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/webhooks")]
public sealed class WebhooksController(IMediator mediator, ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost("stripe")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Stripe(
        [FromBody] StripeWebhookEvent request,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Stripe webhook received without signature verification (Phase 2 gap). Type={Type}",
            request.Type);

        if (!string.Equals(request.Type, "payment_intent.succeeded", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(request.PaymentReference))
        {
            return Accepted();
        }

        var result = await mediator.Send(
            new Commands.CapturePaymentCommand(request.PaymentReference),
            cancellationToken);

        return result.IsSuccess ? Accepted() : BadRequest(result.Errors);
    }
}

public class StripeWebhookEvent
{
    public string? Type { get; set; }

    public string? PaymentReference { get; set; }
}
