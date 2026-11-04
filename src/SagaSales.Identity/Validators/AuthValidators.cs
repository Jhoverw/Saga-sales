namespace SagaSales.Identity.Validators;

using FluentValidation;

public sealed class RegisterTenantCommandValidator : AbstractValidator<Commands.RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-z0-9]([a-z0-9-]*[a-z0-9])?$")
            .WithMessage("Slug must be lowercase letters, digits and hyphens.");

        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);

        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(255);

        RuleFor(x => x.AdminPassword)
            .NotEmpty()
            .MinimumLength(8)
            .WithMessage("Admin password must be at least 8 characters long.");
    }
}

public sealed class LoginCommandValidator : AbstractValidator<Commands.LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
