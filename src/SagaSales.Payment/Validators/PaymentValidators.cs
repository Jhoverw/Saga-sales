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

public sealed class CreatePettyCashExpenseCommandValidator : AbstractValidator<Commands.CreatePettyCashExpenseCommand>
{
    public CreatePettyCashExpenseCommandValidator()
    {
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().Must(d => d.Trim().Length is >= 3 and <= 200)
            .WithMessage("El motivo debe tener entre 3 y 200 caracteres.");
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class SetPettyCashSettingsCommandValidator : AbstractValidator<Commands.SetPettyCashSettingsCommand>
{
    public SetPettyCashSettingsCommandValidator()
    {
        RuleFor(x => x.MaxAmount).GreaterThan(0);
    }
}
