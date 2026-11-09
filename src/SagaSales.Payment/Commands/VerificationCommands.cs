namespace SagaSales.Payment.Commands;

/// <summary>
/// Spec 008: the seller confirms that a pedido's transfer, deposit or card payment really arrived. It needs a
/// receipt. The order is told first; only then is the payment marked, so a refusal from Sales changes nothing here.
/// </summary>
public record VerifyPaymentCommand(Guid PaymentId) : ICommand<bool>;

/// <summary>Spec 008: the money never arrived. The payment fails and stops counting on the order.</summary>
public record RejectPaymentCommand(Guid PaymentId, string Reason) : ICommand<bool>;

public sealed class VerificationHandlers(
    IPaymentRepository repository,
    IOrderingClient ordering,
    ICurrentUser currentUser) :
    ICommandHandler<VerifyPaymentCommand, bool>,
    ICommandHandler<RejectPaymentCommand, bool>
{
    public async Task<Result<bool>> Handle(VerifyPaymentCommand request, CancellationToken cancellationToken)
    {
        var state = await PendingAsync(request.PaymentId, cancellationToken);

        if (state.AttachmentCount == 0)
        {
            throw new ConflictException("Sube el comprobante antes de verificar el pago.");
        }

        await ordering.PaymentVerifiedAsync(state.OrderId, state.Amount, cancellationToken);
        await repository.VerifyAsync(request.PaymentId, currentUser.RequireUserId(), cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> Handle(RejectPaymentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3)
        {
            throw new SagaSales.Shared.Exceptions.ValidationException("Indica el motivo del rechazo.");
        }

        var state = await PendingAsync(request.PaymentId, cancellationToken);

        await ordering.PaymentRejectedAsync(state.OrderId, state.Amount, cancellationToken);
        await repository.RejectAsync(request.PaymentId, currentUser.RequireUserId(), request.Reason.Trim(), cancellationToken);
        return Result<bool>.Success(true);
    }

    private async Task<PaymentVerificationState> PendingAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var state = await repository.GetVerificationStateAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException("Payment", paymentId);

        if (state.VerificationStatus != 1)
        {
            throw new ConflictException("Este pago no está por verificar.");
        }

        return state;
    }
}
