using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Shared.DTOs;
using SagaSales.Sales.Domain;

namespace SagaSales.Sales.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
[Route("api/sales/pre-sales")]
public class PreSaleController : ControllerBase
{
    // Placeholder in‑memory store – replace with repository / DB in production.
    private static readonly List<PreSale> _store = new();

    [HttpPost]
    public Task<ActionResult<PreSaleDto>> Create([FromBody] CreatePreSaleRequest request)
    {
        var pre = new PreSale
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            CustomerId = request.CustomerId,
            Qty = request.Qty,
            ExpectedDate = request.ExpectedDate,
            Status = PreSaleStatus.Pendiente,
            CreatedAt = DateTime.UtcNow,
        };
        _store.Add(pre);
        var dto = new PreSaleDto
        {
            Id = pre.Id,
            ProductId = pre.ProductId,
            CustomerId = pre.CustomerId,
            Qty = pre.Qty,
            ExpectedDate = pre.ExpectedDate,
            Status = pre.Status.ToString(),
        };
        return Task.FromResult<ActionResult<PreSaleDto>>(CreatedAtAction(nameof(Get), new { id = pre.Id }, dto));
    }

    [HttpGet("{id:guid}")]
    public Task<ActionResult<PreSaleDto>> Get(Guid id)
    {
        var pre = _store.Find(p => p.Id == id);
        if (pre == null)
        {
            return Task.FromResult<ActionResult<PreSaleDto>>(NotFound());
        }

        var dto = new PreSaleDto
        {
            Id = pre.Id,
            ProductId = pre.ProductId,
            CustomerId = pre.CustomerId,
            Qty = pre.Qty,
            ExpectedDate = pre.ExpectedDate,
            Status = pre.Status.ToString(),
        };
        return Task.FromResult<ActionResult<PreSaleDto>>(Ok(dto));
    }
}
