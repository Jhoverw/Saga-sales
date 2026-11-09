using Microsoft.AspNetCore.Mvc;
using MediatR;
using SagaSales.EcuadorInvoicing.DTOs;
using SagaSales.EcuadorInvoicing.Commands;
using SagaSales.EcuadorInvoicing.Queries;

namespace SagaSales.EcuadorInvoicing.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
[Route("api/ecuador-invoicing")]
public class EcuadorInvoicingController : ControllerBase
{
    private readonly IMediator _mediator;

    public EcuadorInvoicingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<InvoiceResponseDto>> CreateInvoice([FromBody] CreateInvoiceDto dto)
    {
        var command = new CreateInvoiceCommand(dto);
        var invoice = await _mediator.Send(command);
        var response = new InvoiceResponseDto
        {
            Id = invoice.Id,
            ClaveAcceso = invoice.ClaveAcceso,
            Estado = invoice.Estado,
            MensajeRespuestaSRI = invoice.MensajeRespuestaSRI,
            UrlVerificacion = $"https://sincronizacion.sri.gob.ec/comprobantes-electronicos/{invoice.ClaveAcceso}"
        };
        return Ok(response);
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<InvoiceResponseDto>> GetInvoice(Guid id)
    {
        var invoice = await _mediator.Send(new GetInvoiceQuery(id));
        if (invoice == null) return NotFound();
        var response = new InvoiceResponseDto
        {
            Id = invoice.Id,
            ClaveAcceso = invoice.ClaveAcceso,
            Estado = invoice.Estado,
            MensajeRespuestaSRI = invoice.MensajeRespuestaSRI,
            UrlVerificacion = $"https://sincronizacion.sri.gob.ec/comprobantes-electronicos/{invoice.ClaveAcceso}"
        };
        return Ok(response);
    }
}
