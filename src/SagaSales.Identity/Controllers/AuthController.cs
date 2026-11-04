namespace SagaSales.Identity.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("register-tenant")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterTenantResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterTenant(
        [FromBody] RegisterTenantRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.RegisterTenantCommand(
            request.Slug,
            request.DisplayName,
            request.AdminEmail,
            request.AdminPassword,
            request.CustomDomain);

        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(RegisterTenant), new { id = result.Value!.TenantId }, result.Value);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Commands.LoginCommand(request.Email, request.Password), cancellationToken);
        return Ok(result.Value);
    }
}
