namespace SagaSales.Payment.Infrastructure;

using System.Net.Http.Json;
using SagaSales.Shared.DTOs;

/// <summary>
/// Tells the Sales service that money arrived, so the order's paid amount and status follow the
/// money. Sales owns orders; Payment must not write into its database.
/// </summary>
public interface IOrderingClient
{
    Task RegisterPaymentAsync(Guid orderId, decimal amount, CancellationToken cancellationToken);
}

public sealed class OrderingClient(HttpClient http) : IOrderingClient
{
    public async Task RegisterPaymentAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/orders/{orderId}/payments",
            new RegisterOrderPaymentRequest { Amount = amount },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // Sales refused the money (balance exceeded, order cancelled...): nothing is recorded in Payment.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var reason = string.IsNullOrWhiteSpace(body) ? "the order refused the payment" : body;

        throw response.StatusCode switch
        {
            System.Net.HttpStatusCode.Conflict => new SagaSales.Shared.Exceptions.ConflictException(reason),
            System.Net.HttpStatusCode.BadRequest => new SagaSales.Shared.Exceptions.ValidationException(reason),
            System.Net.HttpStatusCode.NotFound => new SagaSales.Shared.Exceptions.NotFoundException("Order", orderId),
            _ => new InvalidOperationException($"The order service failed ({(int)response.StatusCode}): {body}"),
        };
    }
}

/// <summary>
/// Forwards the caller's bearer token and tenant to the Sales service, so it sees the same identity
/// the gateway authenticated.
/// </summary>
public sealed class ForwardIdentityHandler(IHttpContextAccessor accessor, ICurrentUser currentUser)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext;

        if (context is not null &&
            context.Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization.ToString());
        }

        if (currentUser.TenantId is { } tenantId)
        {
            request.Headers.TryAddWithoutValidation(SagaSalesConstants.Headers.TenantId, tenantId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
