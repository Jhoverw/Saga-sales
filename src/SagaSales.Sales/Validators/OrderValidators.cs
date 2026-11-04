namespace SagaSales.Sales.Validators;

using FluentValidation;

public sealed class CreateOrderCommandValidator : AbstractValidator<Commands.CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("An order needs at least one line.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });

        RuleFor(x => x.ShippingAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
