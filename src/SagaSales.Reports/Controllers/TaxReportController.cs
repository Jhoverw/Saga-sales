using Microsoft.AspNetCore.Mvc;
using SagaSales.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SagaSales.Reports.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
[Route("api/reports")]
public class TaxReportController : ControllerBase
{
    // In‑memory static data for demo – replace with proper DB query.
    private static readonly List<TaxPeriodReportDto> sample = new()
    {
        new TaxPeriodReportDto { Rate = 0.12m, From = new DateTime(2023,1,1), To = new DateTime(2023,6,30), TotalTaxCollected = 12500m },
        new TaxPeriodReportDto { Rate = 0.00m, From = new DateTime(2023,7,1), To = new DateTime(2023,12,31), TotalTaxCollected = 3000m }
    };

    [HttpGet("iva")]
    public Task<ActionResult<IReadOnlyList<TaxPeriodReportDto>> GetIvaReport([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var result = sample.Where(r => r.From <= to && (r.To == null || r.To >= from)).ToList();
        return Task.FromResult<ActionResult<IReadOnlyList<TaxPeriodReportDto>>(Ok(result));
    }
}
