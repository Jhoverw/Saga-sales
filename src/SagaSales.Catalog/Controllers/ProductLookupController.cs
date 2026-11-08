using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;

namespace SagaSales.Catalog.Controllers;

[ApiController]
[Route("api/catalog/products")]
public class ProductLookupController : ControllerBase
{
    // In a real implementation this would call a repository / stored procedure.
    // For now we return a static list for illustration.
    private static readonly List<ProductLookupDto> sample = new()
    {
        new ProductLookupDto { Id = Guid.NewGuid(), CodigoBarras = "1234567890123", Nombre = "Camiseta Algodón", ThumbnailUrl = "images/products/thumb1.png", Stock = 12 },
        new ProductLookupDto { Id = Guid.NewGuid(), CodigoBarras = "9876543210987", Nombre = "Zapatos Cuero", ThumbnailUrl = "images/products/thumb2.png", Stock = 0 }
    };

    [HttpGet("lookup")]
    public Task<ActionResult<IReadOnlyList<ProductLookupDto>>> Lookup([FromQuery] string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length < 3)
        {
            return Task.FromResult<ActionResult<IReadOnlyList<ProductLookupDto>>>(BadRequest("Term must be at least 3 characters"));
        }

        var matches = sample.FindAll(p =>
            (p.CodigoBarras != null && p.CodigoBarras.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
            p.Nombre.Contains(term, StringComparison.OrdinalIgnoreCase));

        // Limit to 5 results as per US V‑01
        var result = matches.Count > 5 ? matches.GetRange(0,5) : matches;
        return Task.FromResult<ActionResult<IReadOnlyList<ProductLookupDto>>>(Ok(result));
    }
}
