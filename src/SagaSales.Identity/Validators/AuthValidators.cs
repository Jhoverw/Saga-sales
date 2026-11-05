namespace SagaSales.Identity.Validators;

using FluentValidation;

public sealed class RegisterTenantCommandValidator : AbstractValidator<Commands.RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        // The slug is normalised in the handler ("Mi Tienda Cafe" -> "mi-tienda-cafe"), so the
        // validator only checks that there is something a slug can be built from. Enforcing the
        // final shape here rejected perfectly valid store names before the handler could clean them.
        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage("Escribe un identificador para la tienda.")
            .MaximumLength(200)
            .Must(value => value is not null && value.Any(char.IsLetterOrDigit))
            .WithMessage("El identificador necesita al menos una letra o un digito.");

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
