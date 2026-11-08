using System.Collections.Generic;
using System.Threading.Tasks;
using SagaSales.Shared.DTOs;

namespace SagaSales.Catalog.Services;

public class ProductLookupService
{
    // In a real implementation this would query the DB via EF or stored procedures.
    public Task<IReadOnlyList<ProductLookupDto>> LookupAsync(string term)
    {
        // Return empty list placeholder.
        return Task.FromResult<IReadOnlyList<ProductLookupDto>>(new List<ProductLookupDto>());
    }
}
