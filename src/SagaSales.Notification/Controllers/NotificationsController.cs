namespace SagaSales.Notification.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = SagaSales.Shared.Constants.SagaSalesRoles.ManagerOrOwner)]
[Route("api/notifications")]
public sealed class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Send(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.SendNotificationCommand(
            request.Type,
            request.Recipient,
            request.Subject,
            request.Body);

        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new Queries.GetNotificationsQuery(pageNumber, pageSize), cancellationToken);
        return Ok(result.Value);
    }
}
