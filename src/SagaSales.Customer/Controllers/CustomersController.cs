namespace SagaSales.Customer.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.CreateCustomerCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.CustomerType,
            request.Address);

        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new Queries.GetCustomersQuery(pageNumber, pageSize, search, includeInactive),
            cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Queries.GetCustomerQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Commands.UpdateCustomerCommand(
            id,
            request.FirstName,
            request.LastName,
            request.Phone,
            request.Status,
            request.CustomerType);

        var result = await mediator.Send(command, cancellationToken);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new Commands.DeleteCustomerCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Adds another shipping address to an existing customer.</summary>
    [HttpPost("{id:guid}/addresses")]
    [ProducesResponseType(typeof(AddressDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAddress(
        Guid id,
        [FromBody] AddressDto address,
        [FromQuery] bool isDefault = false,
        CancellationToken cancellationToken = default)
    {
        var repository = HttpContext.RequestServices.GetRequiredService<Data.ICustomerRepository>();
        var created = await repository.AddAddressAsync(id, address, isDefault, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }
}
