namespace SagaSales.Catalog.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Shared.DTOs;

[ApiController]
[Authorize]
[Route("api/branches")]
public sealed class BranchesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.CreateBranchCommand(request), cancellationToken);
        return CreatedAtAction(nameof(List), null, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BranchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetBranchesQuery(includeInactive), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.UpdateBranchCommand(id, request), cancellationToken);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.DeactivateBranchCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>The "Parámetros" screen: expiry warning threshold and default branch.</summary>
[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class SettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetSettingsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut]
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.UpdateSettingsCommand(request), cancellationToken);
        return Ok(result.Value);
    }
}

/// <summary>Per-branch stock, physical location and lots about to expire.</summary>
[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController(IMediator mediator) : ControllerBase
{
    [HttpGet("products/{productId:guid}/branches")]
    [ProducesResponseType(typeof(IReadOnlyList<StockByBranchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StockByBranch(Guid productId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetStockByBranchQuery(productId), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("products/{productId:guid}/location")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetLocation(
        Guid productId,
        [FromBody] SetLocationRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.SetLocationCommand(productId, request), cancellationToken);
        return NoContent();
    }

    [HttpGet("expiring")]
    [ProducesResponseType(typeof(IReadOnlyList<ExpiringBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Expiring(
        [FromQuery] Guid? branchId = null,
        [FromQuery] int maxRows = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetExpiringBatchesQuery(branchId, maxRows), cancellationToken);
        return Ok(result.Value);
    }
}
