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
    Guid CustomerId = default,
    decimal? CashReceived = null,
    bool RequiresVerification = false) : ICommand<PaymentDto>;

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

        // FR-016: Sales decides first. It refuses an amount above the balance (409) and then nothing is
        // recorded here. Only an accepted amount becomes a payment.
        await repository.AssertNotDuplicateAsync(request.OrderId, method, request.Amount, cancellationToken);
        // Spec 008: a pedido paid by card, transfer or deposit counts as paid but not verified until checked.
        var pendingCheck = request.RequiresVerification && method != ManualPaymentMethod.Cash;
        await ordering.RegisterPaymentAsync(request.OrderId, request.Amount, !pendingCheck, cancellationToken);

        var payment = await repository.CreateManualAsync(
            request.OrderId,
            request.CustomerId,
            request.Amount,
            method,
            request.Reference,
            request.PaidAt,
            request.CashReceived,
            cancellationToken,
            currentUser.UserId);

        if (pendingCheck)
        {
            await repository.RequireVerificationAsync(payment.Id, cancellationToken);
            payment.VerificationStatus = 1;
        }

        return Result<PaymentDto>.Success(payment);
    }

    private static ManualPaymentMethod ToManualMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.CashOnDelivery => ManualPaymentMethod.Cash,
        PaymentMethod.CreditCard => ManualPaymentMethod.Card,
        _ => ManualPaymentMethod.Transfer,
    };
}
