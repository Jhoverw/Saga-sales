namespace SagaSales.Catalog.Validators;

using FluentValidation;

public sealed class CreateProductCommandValidator : AbstractValidator<Commands.CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        // FR-001: the title must be at least 2 characters.
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(255);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).Length(3);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

public sealed class UpdateProductCommandValidator : AbstractValidator<Commands.UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(255);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).Length(3);
    }
}
