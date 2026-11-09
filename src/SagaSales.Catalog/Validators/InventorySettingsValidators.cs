namespace SagaSales.Catalog.Validators;

using FluentValidation;

/// <summary>Spec 004: lengths match db/catalog/V024; numbers cannot be negative.</summary>
public sealed class SetInventorySettingsCommandValidator : AbstractValidator<Commands.SetInventorySettingsCommand>
{
    public SetInventorySettingsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Code).MaximumLength(50);
        RuleFor(x => x.Request.Manufacturer).MaximumLength(150);
        RuleFor(x => x.Request.Notes).MaximumLength(500);
        RuleFor(x => x.Request.ReorderLevel).GreaterThanOrEqualTo(0).When(x => x.Request.ReorderLevel.HasValue);
        RuleFor(x => x.Request.ReorderQuantity).GreaterThanOrEqualTo(0).When(x => x.Request.ReorderQuantity.HasValue);
        RuleFor(x => x.Request.LeadTimeDays).GreaterThanOrEqualTo(0).When(x => x.Request.LeadTimeDays.HasValue);
    }
}
