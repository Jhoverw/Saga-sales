namespace SagaSales.Catalog.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/products")]
public sealed class ProductsController(IMediator mediator) : ControllerBase
{
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
            request.TracksBatches);

        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetProductQuery(id), cancellationToken);
        return Ok(result.Value);
    }

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

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.DeleteProductCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Sets the price for one customer type (D5).</summary>
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

    /// <summary>
    /// The price that applies to a customer type, telling the caller whether the list price or the
    /// fallback base price was used (FR-007b).
    /// </summary>
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

    /// <summary>
    /// Atomically reserves stock. Called by the Sales service so it never touches this database.
    /// </summary>
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

    /// <summary>Compensating operation for <see cref="ReserveStock"/> when checkout aborts.</summary>
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

    /// <summary>
    /// The kardex of a product: every purchase that brought stock in and every sale that took it
    /// out, with the resulting quantity (E-12).
    /// </summary>
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
}
