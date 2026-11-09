namespace SagaSales.Payment.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Payment.Commands;
using SagaSales.Shared.Time;

/// <summary>
/// Panel "Mi día" (spec 006): cobros por forma de pago y caja chica. Vive bajo <c>api/payments</c> para
/// usar la ruta del gateway que ya existe.
/// </summary>
[ApiController]
[Authorize(Roles = SagaSalesRoles.Staff)]
[Route("api/payments")]
public sealed class MyDayController(IMediator mediator) : ControllerBase
{
    /// <summary>Cobros netos de los últimos <paramref name="days"/> días locales y caja chica del mismo rango.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(PaymentDaySummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(
        [FromQuery] int days = 1,
        [FromQuery] int offsetMinutes = 0,
        [FromQuery] Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryRange(days, offsetMinutes, out var range))
        {
            return BadRequest(new { message = "Rango de días o desfase horario fuera de rango." });
        }

        var result = await mediator.Send(new GetPaymentDaySummaryQuery(range, userId, OnlyCaller()), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("petty-cash")]
    [ProducesResponseType(typeof(IReadOnlyList<PettyCashExpenseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPettyCash(
        [FromQuery] int days = 1,
        [FromQuery] int offsetMinutes = 0,
        [FromQuery] Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryRange(days, offsetMinutes, out var range))
        {
            return BadRequest(new { message = "Rango de días o desfase horario fuera de rango." });
        }

        var result = await mediator.Send(new GetPettyCashQuery(range, userId, OnlyCaller()), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Registra un gasto pagado con el efectivo de la caja. Sobre el máximo de la tienda → 400.</summary>
    [HttpPost("petty-cash")]
    [ProducesResponseType(typeof(PettyCashExpenseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePettyCash(
        [FromBody] CreatePettyCashExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreatePettyCashExpenseCommand(request.Category, request.Description ?? string.Empty, request.Amount),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Roles = SagaSalesRoles.ManagerOrOwner)]
    [HttpPost("petty-cash/{id:guid}/void")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> VoidPettyCash(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new VoidPettyCashExpenseCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("petty-cash/settings")]
    [ProducesResponseType(typeof(PettyCashSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPettyCashSettingsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [Authorize(Roles = SagaSalesRoles.Owner)]
    [HttpPut("petty-cash/settings")]
    [ProducesResponseType(typeof(PettyCashSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetSettings(
        [FromBody] PettyCashSettingsDto request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetPettyCashSettingsCommand(request.MaxAmount), cancellationToken);
        return Ok(result.Value);
    }

    private static bool TryRange(int days, int offsetMinutes, out LocalDayRange range)
    {
        range = default;

        if (days is < 1 or > LocalDayRange.MaxDays || offsetMinutes is < -840 or > 840)
        {
            return false;
        }

        range = LocalDayRange.LastDays(days, offsetMinutes, DateTime.UtcNow);
        return true;
    }

    private bool OnlyCaller() => !User.IsInRole(SagaSalesRoles.Owner) && !User.IsInRole(SagaSalesRoles.Manager);
}
