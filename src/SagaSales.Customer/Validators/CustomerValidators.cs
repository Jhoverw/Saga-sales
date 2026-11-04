namespace SagaSales.Customer.Validators;

using FluentValidation;

public sealed class CreateCustomerCommandValidator : AbstractValidator<Commands.CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Phone).MaximumLength(30);

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Line1).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Address!.City).NotEmpty().MaximumLength(100);
        });
    }
}

public sealed class UpdateCustomerCommandValidator : AbstractValidator<Commands.UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Status).IsInEnum();
    }
}
