namespace SagaSales.Identity.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>The owner manages the store's staff users (spec 005). Owner only.</summary>
[ApiController]
[Authorize(Roles = SagaSalesRoles.Owner)]
[Route("api/users")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.GetUsersQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.CreateUserCommand(request), cancellationToken);
        return Created($"api/users/{result.Value!.Id}", result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.UpdateUserCommand(id, request), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.ResetPasswordCommand(id, request), cancellationToken);
        return NoContent();
    }
}
