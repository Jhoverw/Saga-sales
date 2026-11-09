namespace SagaSales.Identity.Validators;

using FluentValidation;

/// <summary>Spec 005: valid email, name, one of the three staff roles, password of 8+ characters.</summary>
public sealed class CreateUserCommandValidator : AbstractValidator<Commands.CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Request.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Role).Must(role => SagaSalesRoles.All.Contains(role))
            .WithMessage("El rol debe ser admin, manager o sales.");
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class UpdateUserCommandValidator : AbstractValidator<Commands.UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Role).Must(role => SagaSalesRoles.All.Contains(role))
            .WithMessage("El rol debe ser admin, manager o sales.");
    }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<Commands.ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
