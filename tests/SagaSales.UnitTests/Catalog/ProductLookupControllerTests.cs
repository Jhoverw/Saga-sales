using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SagaSales.Catalog.Controllers;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.UnitTests.Catalog;

public class ProductLookupControllerTests
{
    [Fact]
    public async Task Lookup_Returns_Matching_Products()
    {
        // Arrange – controller has a static sample list.
        var controller = new ProductLookupController();
        var term = "Cami"; // matches "Camiseta Algodón"

        // Act
        var result = await controller.Lookup(term);

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ProductLookupDto>>(okResult.Value);
        Assert.Single(list);
        Assert.Contains("Camiseta", list[0].Nombre);
    }

    [Fact]
    public async Task Lookup_Term_Under_Three_Chars_Returns_BadRequest()
    {
        var controller = new ProductLookupController();
        var result = await controller.Lookup("ab");
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }
}
