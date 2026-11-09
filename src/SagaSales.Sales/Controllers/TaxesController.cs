namespace SagaSales.Sales.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/taxes")]
public sealed class TaxesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetTaxRatesQuery(), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>V-03: the VAT rate in force on <paramref name="date"/> (UTC today when omitted).</summary>
    [HttpGet("current")]
    public async Task<IActionResult> Current([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await mediator.Send(new Queries.GetCurrentTaxRateQuery(day), cancellationToken);
        if (result.Value is null)
        {
            return NotFound(new { error = "NOT_FOUND", message = $"No VAT rate is in force on {day:yyyy-MM-dd}." });
        }

        return Ok(result.Value);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SagaSales.Shared.DTOs.CreateTaxRateRequest request, CancellationToken cancellationToken)
    {
        var command = new Commands.CreateTaxRateCommand(
            request.RatePercentage,
            request.StartDate,
            request.EndDate,
            request.Description);

        var result = await mediator.Send(command, cancellationToken);
        return Ok(result.Value);
    }
}
