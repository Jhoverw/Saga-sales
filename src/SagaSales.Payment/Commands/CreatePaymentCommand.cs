namespace SagaSales.Payment.Commands;

using SagaSales.Payment.Domain;
using SagaSales.Payment.Infrastructure;

/// <summary>
/// Authorizes a payment for an order. The order comes from the Payment-owned read model,
/// not from the Sales database.
/// </summary>
public record CreatePaymentCommand(Guid OrderId, PaymentMethod Method, string? GatewayReference = null)
    : ICommand<PaymentDto>;

public sealed class CreatePaymentCommandHandler(
    PaymentDbContext dbContext,
    IPaymentGateway gateway,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<CreatePaymentCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.OrderId, nameof(request.OrderId));

        var tenantId = currentUser.RequireTenantId();

        var order = await dbContext.OrderReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException("Order", request.OrderId);
        }

        var alreadyPaid = await dbContext.Payments
            .AnyAsync(
                p => p.OrderId == request.OrderId &&
                     (p.Status == PaymentStatus.Captured || p.Status == PaymentStatus.Authorized),
                cancellationToken);

        if (alreadyPaid)
        {
            throw new ConflictException($"Order {request.OrderId} already has an authorized payment.");
        }

        var gatewayResult = await gateway.AuthorizeAsync(
            new GatewayRequest(order.OrderId, tenantId, order.Subtotal, order.Currency, request.Method),
            cancellationToken);

        var payment = new Payment
        {
            TenantId = tenantId,
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            Method = request.Method,
            Amount = order.Subtotal,
            Currency = order.Currency,
        };

        if (!gatewayResult.Succeeded)
        {
            payment.MarkFailed(gatewayResult.FailureReason);
            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new PaymentFailedException(gatewayResult.FailureReason ?? "The payment was declined.");
        }

        payment.MarkAuthorized(
            request.GatewayReference ?? gatewayResult.TransactionReference ?? string.Empty,
            gatewayResult.RawResponse);

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new PaymentAuthorizedEvent(
                payment.Id,
                payment.OrderId,
                payment.TenantId,
                payment.Method,
                payment.Amount,
                payment.PaidAt ?? DateTime.UtcNow),
            cancellationToken);

        return Result<PaymentDto>.Success(payment.ToDto());
    }
}
