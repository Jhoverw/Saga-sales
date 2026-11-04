namespace SagaSales.Payment.Validators;

using FluentValidation;

public sealed class CreatePaymentCommandValidator : AbstractValidator<Commands.CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Method).IsInEnum();
    }
}

public sealed class RefundPaymentCommandValidator : AbstractValidator<Commands.RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.RefundAmount).GreaterThan(0);
    }
}
