namespace SagaSales.Catalog.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Shared.DTOs;

[ApiController]
[Authorize]
[Route("api/suppliers")]
public sealed class SuppliersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.CreateSupplierCommand(request), cancellationToken);
        return CreatedAtAction(nameof(List), null, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetSuppliersQuery(includeInactive, search), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.UpdateSupplierCommand(id, request), cancellationToken);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.DeactivateSupplierCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/purchases")]
public sealed class PurchasesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.CreatePurchaseCommand(
            request.SupplierId,
            request.Lines.ToList(),
            request.SupplierInvoiceNumber,
            request.PurchasedAt,
            request.Notes);

        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PurchaseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetPurchasesQuery(pageNumber, pageSize, status), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetPurchaseQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Receives the purchase: stock in, weighted average cost recalculated and kardex written.
    /// A second call returns 409.
    /// </summary>
    [HttpPost("{id:guid}/receive")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Receive(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.ReceivePurchaseCommand(id), cancellationToken);
        return Ok(result.Value);
    }
}
