namespace SagaSales.Payment.Commands;

using SagaSales.Payment.Data;
using SagaSales.Payment.Infrastructure;

/// <summary>
/// Records money received outside the platform (E-04a): a transfer, a deposit or cash.
/// The payment is captured immediately because the money is already in hand, and then the order is
/// told, so what the customer paid and what the order says cannot drift apart.
/// </summary>
public record CreatePaymentCommand(
    Guid OrderId,
    PaymentMethod Method,
    decimal Amount = 0,
    string? Reference = null,
    DateTime? PaidAt = null,
    Guid CustomerId = default) : ICommand<PaymentDto>;

public sealed class CreatePaymentCommandHandler(
    IPaymentRepository repository,
    IOrderingClient ordering,
    ICurrentUser currentUser) : ICommandHandler<CreatePaymentCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.OrderId, nameof(request.OrderId));
        Guard.AgainstNegativeOrZero(request.Amount, nameof(request.Amount));

        var method = ToManualMethod(request.Method);

        var payment = await repository.CreateManualAsync(
            request.OrderId,
            request.CustomerId,
            request.Amount,
            method,
            request.Reference,
            request.PaidAt,
            cancellationToken);

        // Sales owns the order: it grows paid_amount and moves a fully paid order to processing.
        await ordering.RegisterPaymentAsync(request.OrderId, request.Amount, cancellationToken);

        _ = currentUser;
        return Result<PaymentDto>.Success(payment);
    }

    private static ManualPaymentMethod ToManualMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.CashOnDelivery => ManualPaymentMethod.Cash,
        _ => ManualPaymentMethod.Transfer,
    };
}
