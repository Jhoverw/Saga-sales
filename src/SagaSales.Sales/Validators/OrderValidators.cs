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

        // Spec 008: 1 mostrador, 2 pedido; contra entrega solo existe en pedidos.
        RuleFor(x => x.Channel).InclusiveBetween(1, 2);
        RuleFor(x => x.CashOnDelivery).Equal(false).When(x => x.Channel == 1)
            .WithMessage("Contra entrega solo aplica a pedidos.");
    }
}
