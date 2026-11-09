namespace SagaSales.Catalog.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Shared.DTOs;

[ApiController]
[Authorize]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
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
[Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
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
            SupplierId: request.SupplierId,
            Lines: request.Lines.ToList(),
            BranchId: request.BranchId,
            ReceiveNow: request.ReceiveNow,
            SupplierInvoiceNumber: request.SupplierInvoiceNumber,
            PurchasedAt: request.PurchasedAt,
            Notes: request.Notes);

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

    /// <summary>Registra la compra como viene la factura del proveedor (spec 009). Comprobante repetido → 409.</summary>
    [HttpPost("invoice")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] PurchaseInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.CreatePurchaseInvoiceCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Busca productos por código o nombre para el detalle de la compra.</summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(IReadOnlyList<PurchaseProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FindProducts(
        [FromQuery] string? term,
        [FromQuery] int max = 15,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.FindPurchaseProductsQuery(term ?? string.Empty, max), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Crea un producto nuevo desde la línea de la compra (sin stock; lo pone la compra).</summary>
    [HttpPost("products")]
    [ProducesResponseType(typeof(PurchaseProductDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreatePurchaseProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.CreatePurchaseProductCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Cuentas por pagar a proveedores; por defecto solo las que tienen saldo.</summary>
    [HttpGet("payables")]
    [ProducesResponseType(typeof(IReadOnlyList<PayableDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Payables(
        [FromQuery] bool includePaid = false,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetPayablesQuery(includePaid), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("payables/{id:guid}/payments")]
    [ProducesResponseType(typeof(IReadOnlyList<PayablePaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PayablePayments(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetPayablePaymentsQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Abono a la cuenta por pagar. Más que el saldo o ya pagada → 409.</summary>
    [HttpPost("payables/{id:guid}/payments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Pay(
        Guid id,
        [FromBody] PayablePaymentRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.PayPayableCommand(id, request), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Receives the purchase: stock in, weighted average cost recalculated and kardex written.
    /// A second call returns 409.
    /// </summary>
    [HttpPost("{id:guid}/receive")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Receive(
        Guid id,
        [FromQuery] Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Commands.ReceivePurchaseCommand(id, branchId), cancellationToken);
        return Ok(result.Value);
    }
}
