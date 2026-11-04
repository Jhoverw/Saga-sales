namespace SagaSales.Payment.Commands;

public record CapturePaymentCommand(string TransactionReference) : ICommand;

public sealed class CapturePaymentCommandHandler(
    PaymentDbContext dbContext,
    IEventBus eventBus) : ICommandHandler<CapturePaymentCommand>
{
    public async Task<Result> Handle(CapturePaymentCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.TransactionReference, nameof(request.TransactionReference));

        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.TransactionReference == request.TransactionReference, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException("Payment", request.TransactionReference);
        }

        payment.MarkCaptured();
        await dbContext.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new PaymentCapturedEvent(
                payment.Id,
                payment.OrderId,
                payment.TenantId,
                payment.CapturedAt ?? DateTime.UtcNow),
            cancellationToken);

        return Result.Success();
    }
}
