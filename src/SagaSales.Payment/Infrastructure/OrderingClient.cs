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

    /// <summary>Spec 008: <paramref name="verified"/> false para un pago de pedido que falta confirmar.</summary>
    Task RegisterPaymentAsync(Guid orderId, decimal amount, bool verified, CancellationToken cancellationToken);

    Task PaymentVerifiedAsync(Guid orderId, decimal amount, CancellationToken cancellationToken);

    Task PaymentRejectedAsync(Guid orderId, decimal amount, CancellationToken cancellationToken);
}

public sealed class OrderingClient(HttpClient http) : IOrderingClient
{
    public Task RegisterPaymentAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
        => RegisterPaymentAsync(orderId, amount, verified: true, cancellationToken);

    public async Task RegisterPaymentAsync(Guid orderId, decimal amount, bool verified, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/orders/{orderId}/payments",
            new RegisterOrderPaymentRequest { Amount = amount, Verified = verified },
            cancellationToken);

        await EnsureAcceptedAsync(response, orderId, cancellationToken);
    }

    public async Task PaymentVerifiedAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/orders/{orderId}/payments/verified",
            new OrderPaymentAdjustmentRequest { Amount = amount },
            cancellationToken);

        await EnsureAcceptedAsync(response, orderId, cancellationToken);
    }

    public async Task PaymentRejectedAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/orders/{orderId}/payments/rejected",
            new OrderPaymentAdjustmentRequest { Amount = amount },
            cancellationToken);

        await EnsureAcceptedAsync(response, orderId, cancellationToken);
    }

    private static async Task EnsureAcceptedAsync(HttpResponseMessage response, Guid orderId, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // Sales refused the money (balance exceeded, order cancelled...): nothing is recorded in Payment.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var reason = MessageOf(body) ?? "the order refused the payment";

        throw response.StatusCode switch
        {
            System.Net.HttpStatusCode.Conflict => new SagaSales.Shared.Exceptions.ConflictException(reason),
            System.Net.HttpStatusCode.BadRequest => new SagaSales.Shared.Exceptions.ValidationException(reason),
            System.Net.HttpStatusCode.NotFound => new SagaSales.Shared.Exceptions.NotFoundException("Order", orderId),
            _ => new InvalidOperationException($"The order service failed ({(int)response.StatusCode}): {body}"),
        };
    }

    // Sales answers {"code","message"}: pass its message on, not the raw JSON.
    private static string? MessageOf(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                   && document.RootElement.TryGetProperty("message", out var message)
                   && message.ValueKind == System.Text.Json.JsonValueKind.String
                ? message.GetString()
                : body;
        }
        catch (System.Text.Json.JsonException)
        {
            return body;
        }
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
