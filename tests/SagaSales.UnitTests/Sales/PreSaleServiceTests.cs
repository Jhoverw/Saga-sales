using System;
using System.Threading.Tasks;
using SagaSales.Sales.Domain;
using SagaSales.Sales.Services;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.UnitTests.Sales;

public class PreSaleServiceTests
{
    [Fact]
    public async Task Create_And_Get_PreSale_Work()
    {
        var service = new PreSaleService();
        var request = new CreatePreSaleRequest
        {
            ProductId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Qty = 5,
            ExpectedDate = DateTime.UtcNow.AddDays(10),
        };

        var created = await service.CreateAsync(request);
        Assert.NotNull(created);
        Assert.Equal(request.Qty, created.Qty);

        var fetched = await service.GetAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
    }
}
