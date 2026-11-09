namespace SagaSales.Payment.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Payment.Commands;

/// <summary>Comprobantes de pago (spec 007). No son públicos: se piden con sesión de la misma tienda.</summary>
[ApiController]
[Authorize(Roles = SagaSalesRoles.Staff)]
[Route("api/payments")]
public sealed class AttachmentsController(IMediator mediator) : ControllerBase
{
    [HttpPost("{paymentId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(PaymentAttachmentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload(
        Guid paymentId,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { code = "ValidationError", message = "Selecciona la foto o el PDF del comprobante." });
        }

        if (file.Length > Services.ReceiptFileType.MaxBytes)
        {
            return BadRequest(new { code = "ValidationError", message = "El comprobante no puede superar los 5 MB." });
        }

        byte[] content;
        await using (var stream = file.OpenReadStream())
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        var result = await mediator.Send(new UploadPaymentAttachmentCommand(paymentId, file.FileName, content), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("{paymentId:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPaymentAttachmentsQuery(paymentId), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("attachments/{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DownloadPaymentAttachmentQuery(id), cancellationToken);
        var file = result.Value!;

        // nosniff: el navegador respeta el tipo detectado y no interpreta el archivo como otra cosa.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Lo consulta Sales antes de marcar un pedido como enviado (FR-003).</summary>
    [HttpGet("proof-status")]
    [ProducesResponseType(typeof(OrderProofStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProofStatus([FromQuery] Guid orderId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrderProofStatusQuery(orderId), cancellationToken);
        return Ok(result.Value);
    }
}
