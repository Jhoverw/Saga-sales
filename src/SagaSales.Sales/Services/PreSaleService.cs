using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SagaSales.Sales.Domain;
using SagaSales.Shared.DTOs;

namespace SagaSales.Sales.Services;

public class PreSaleService
{
    private readonly List<PreSale> _store = new();

    public Task<PreSaleDto> CreateAsync(CreatePreSaleRequest request)
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
        return Task.FromResult(dto);
    }

    public Task<PreSaleDto?> GetAsync(Guid id)
    {
        var pre = _store.Find(p => p.Id == id);
        if (pre == null)
        {
            return Task.FromResult<PreSaleDto?>(null);
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
        return Task.FromResult<PreSaleDto?>(dto);
    }
}
