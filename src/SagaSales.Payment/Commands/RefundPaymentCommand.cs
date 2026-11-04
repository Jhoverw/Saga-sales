namespace SagaSales.Payment.Commands;

/// <summary>
/// Refunds a payment fully or partially. The previous handler mutated the status without
/// validating the amount and used an <c>eventBus</c> that was never injected.
/// </summary>
public record RefundPaymentCommand(Guid PaymentId, decimal RefundAmount) : ICommand<PaymentDto>;

public sealed class RefundPaymentCommandHandler(
    PaymentDbContext dbContext,
    IEventBus eventBus,
    ICurrentUser currentUser) : ICommandHandler<RefundPaymentCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.PaymentId, nameof(request.PaymentId));
        Guard.AgainstNegativeOrZero(request.RefundAmount, nameof(request.RefundAmount));

        var tenantId = currentUser.RequireTenantId();

        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId && p.TenantId == tenantId, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException("Payment", request.PaymentId);
        }

        payment.Refund(request.RefundAmount);
        await dbContext.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new PaymentRefundedEvent(
                payment.Id,
                payment.OrderId,
                payment.TenantId,
                request.RefundAmount,
                payment.RefundedAt ?? DateTime.UtcNow),
            cancellationToken);

        return Result<PaymentDto>.Success(payment.ToDto());
    }
}
