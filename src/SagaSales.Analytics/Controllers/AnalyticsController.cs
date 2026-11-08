namespace SagaSales.Analytics.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Analytics.Services;

[ApiController]
[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController(IMediator mediator, ISalesSummaryCsvExporter csvExporter) : ControllerBase
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

    [HttpGet("sales-summary.csv")]
    public async Task<IActionResult> SalesSummaryCsv(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var tenantGuid))
        {
            return BadRequest("Tenant ID missing or invalid");
        }

        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dateFrom = from ?? dateTo.AddDays(-6);

        var csvData = await csvExporter.ExportAsync(tenantGuid, dateFrom, dateTo, cancellationToken);
        
        return File(csvData, "text/csv", $"sales-summary-{dateFrom:yyyy-MM-dd}.csv");
    }

    [HttpGet("daily")]
    [ProducesResponseType(typeof(IReadOnlyList<DailyPointDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Daily(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetDailySeriesQuery(from, to), cancellationToken);
        return Ok(result.Value);
    }
}

