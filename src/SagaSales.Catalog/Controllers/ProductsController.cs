namespace SagaSales.Catalog.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Catalog.Services;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(IMediator mediator, IProductImageStorage imageStorage) : ControllerBase
{
    /// <summary>
    /// Buscador único (spec 010): código de barras, código interno o nombre, desde 2 caracteres. Un código exacto
    /// viene con <c>IsExactMatch</c> y las unidades del código (la caja). <c>purpose=purchase</c> deja solo bienes.
    /// </summary>
    [Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] string? purpose = null,
        [FromQuery] int max = 10,
        CancellationToken cancellationToken = default)
    {
        var showCost = User.IsInRole(SagaSales.Shared.Constants.SagaSalesRoles.Owner)
                       || User.IsInRole(SagaSales.Shared.Constants.SagaSalesRoles.Manager);
        var forPurchase = string.Equals(purpose, "purchase", StringComparison.OrdinalIgnoreCase);
        var result = await mediator.Send(
            new Queries.SearchProductsQuery(term ?? string.Empty, forPurchase, showCost, max), cancellationToken);
        return Ok(result.Value);
    }

    [Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
    [HttpGet("{id:guid}/barcodes")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductBarcodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Barcodes(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetProductBarcodesQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Agrega un código de barras al producto. Código de otro producto → 409.</summary>
    [Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPost("{id:guid}/barcodes")]
    [ProducesResponseType(typeof(ProductBarcodeDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddBarcode(
        Guid id,
        [FromBody] AddProductBarcodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.AddProductBarcodeCommand(id, request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpDelete("{id:guid}/barcodes/{barcodeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveBarcode(Guid id, Guid barcodeId, CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.RemoveProductBarcodeCommand(id, barcodeId), cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool descending = false,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new Queries.GetProductsQuery(pageNumber, pageSize, categoryId, search, sortBy, descending),
            cancellationToken);

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetProductQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/price")]
    [ProducesResponseType(typeof(PriceQuoteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrice(
        Guid id,
        [FromQuery] CustomerType customerType = CustomerType.Retail,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetProductPriceQuery(id, customerType), cancellationToken);
        return Ok(result.Value);
    }

    [Authorize]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var command = new Commands.CreateProductCommand(
            request.Name,
            request.Description,
            request.Price,
            request.Currency,
            request.StockQuantity,
            request.CategoryId,
            request.TracksBatches,
            request.IsTaxable);

        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.UpdateProductCommand(
            id,
            request.Name,
            request.Description,
            request.Price,
            request.Currency,
            request.StockQuantity,
            request.CategoryId,
            ProductStatus.Active,
            request.TracksBatches);

        var result = await mediator.Send(command, cancellationToken);
        return Ok(result.Value);
    }

    [Authorize]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.DeleteProductCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPut("{id:guid}/prices/{customerType}")]
    [ProducesResponseType(typeof(PriceQuoteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPrice(
        Guid id,
        CustomerType customerType,
        [FromBody] SetPriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new Commands.SetProductPriceCommand(id, customerType, request.Price),
            cancellationToken);

        return Ok(result.Value);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
    [HttpPost("{id:guid}/stock/reserve")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReserveStock(
        Guid id,
        [FromBody] ReserveStockRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new Commands.ReserveStockCommand(id, request.Quantity, request.BranchId),
            cancellationToken);

        return Ok(result.Value);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.Staff)]
    [HttpPost("{id:guid}/stock/release")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseStock(
        Guid id,
        [FromBody] ReserveStockRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.ReleaseStockCommand(id, request.Quantity, request.BranchId), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpGet("{id:guid}/movements")]
    [ProducesResponseType(typeof(IReadOnlyList<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Movements(
        Guid id,
        [FromQuery] int maxRows = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetStockMovementsQuery(id, maxRows), cancellationToken);
        return Ok(result.Value);
    }

    [Authorize]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(
        Guid id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new { code = "ValidationError", message = "Selecciona una imagen para subir." });
        }

        var imagePath = await imageStorage.SaveAsync(id, file, cancellationToken);

        var result = await mediator.Send(new Commands.SetProductImageCommand(id, imagePath), cancellationToken);
        return Ok(result.Value);
    }
}
