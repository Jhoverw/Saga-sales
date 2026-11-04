namespace SagaSales.Payment.Infrastructure;

/// <summary>
/// Payment gateway abstraction. The previous code called a Stripe SDK that was never
/// installed and a <c>PayPalClient</c> type that does not exist anywhere.
/// Phase 2 plugs the real Stripe/PayPal SDKs in behind this interface.
/// </summary>
public interface IPaymentGateway
{
    Task<GatewayResult> AuthorizeAsync(GatewayRequest request, CancellationToken cancellationToken);
}

public record GatewayRequest(
    Guid OrderId,
    Guid TenantId,
    decimal Amount,
    string Currency,
    PaymentMethod Method);

public record GatewayResult(bool Succeeded, string? TransactionReference, string? RawResponse, string? FailureReason);

/// <summary>
/// Development gateway: authorizes every request and logs that no real provider is wired.
/// It never moves money, so it is safe by default.
/// </summary>
public sealed class StubPaymentGateway(ILogger<StubPaymentGateway> logger) : IPaymentGateway
{
    public Task<GatewayResult> AuthorizeAsync(GatewayRequest request, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "No payment provider configured: authorizing order {OrderId} for {Amount} {Currency} with the stub gateway.",
            request.OrderId,
            request.Amount,
            request.Currency);

        var reference = $"STUB-{Guid.NewGuid():N}";
        var raw = $"{{\"provider\":\"stub\",\"method\":\"{request.Method}\",\"reference\":\"{reference}\"}}";

        return Task.FromResult(new GatewayResult(true, reference, raw, null));
    }
}
