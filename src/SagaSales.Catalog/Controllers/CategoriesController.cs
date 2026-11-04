namespace SagaSales.Catalog.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetCategoriesQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.CreateCategoryCommand(request.Name, request.Slug, request.ParentId);
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = result.Value!.Id }, result.Value);
    }
}
