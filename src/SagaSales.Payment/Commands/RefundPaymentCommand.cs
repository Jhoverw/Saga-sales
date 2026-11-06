namespace SagaSales.Payment.Commands;

using SagaSales.Payment.Data;

/// <summary>
/// Returns money, fully or partially. The procedure decides whether the payment becomes refunded
/// or partially refunded, and refuses to go past what was charged (FR-017).
/// </summary>
public record RefundPaymentCommand(Guid PaymentId, decimal RefundAmount) : ICommand<PaymentDto>;

public sealed class RefundPaymentCommandHandler(IPaymentRepository repository)
    : ICommandHandler<RefundPaymentCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.PaymentId, nameof(request.PaymentId));
        Guard.AgainstNegativeOrZero(request.RefundAmount, nameof(request.RefundAmount));

        var payment = await repository.RefundAsync(request.PaymentId, request.RefundAmount, cancellationToken);
        return Result<PaymentDto>.Success(payment);
    }
}
