namespace SagaSales.Analytics.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController(IMediator mediator) : ControllerBase
{
    [HttpGet("sales-summary")]
    [ProducesResponseType(typeof(SalesSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SalesSummary(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetSalesSummaryQuery(from, to), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("daily")]
    [ProducesResponseType(typeof(IReadOnlyList<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Daily(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetDailySeriesQuery(from, to), cancellationToken);
        return Ok(result.Value);
    }
}
