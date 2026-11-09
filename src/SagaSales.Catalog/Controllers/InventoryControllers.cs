namespace SagaSales.Catalog.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Shared.DTOs;

[ApiController]
[Authorize]
[Route("api/branches")]
public sealed class BranchesController(IMediator mediator) : ControllerBase
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
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

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
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

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
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

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
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
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpGet("products/{productId:guid}/branches")]
    [ProducesResponseType(typeof(IReadOnlyList<StockByBranchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StockByBranch(Guid productId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetStockByBranchQuery(productId), cancellationToken);
        return Ok(result.Value);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
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

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
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

    /// <summary>Panel "Mi día" (spec 006): lotes por vencer y productos bajo su nivel de reorden, sin costos.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(InventoryAlertsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Alerts([FromQuery] Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetInventoryAlertsQuery(branchId), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Inventory control report (spec 004): what to restock first, value, last sale, supplier.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpGet("report")]
    [ProducesResponseType(typeof(IReadOnlyList<InventoryReportRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Report([FromQuery] Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetInventoryReportQuery(branchId), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>The same report as CSV: semicolons, decimal point and UTF-8 BOM so Excel opens it directly.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpGet("report.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> ReportCsv([FromQuery] Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetInventoryReportQuery(branchId), cancellationToken);
        var text = Services.InventoryReportCsv.Build(result.Value ?? []);
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(text))
            .ToArray();

        return File(bytes, "text/csv", $"control-inventario-{SagaSales.Shared.Time.BusinessTime.Now:yyyyMMdd}.csv");
    }

    /// <summary>Reorder data of a product; never changes price or stock (spec 004, FR-008).</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPut("products/{productId:guid}/settings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetSettings(
        Guid productId,
        [FromBody] InventorySettingsRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.SetInventorySettingsCommand(productId, request), cancellationToken);
        return NoContent();
    }
}
