namespace SagaSales.Customer.Validators;

using FluentValidation;
using SagaSales.Shared.Validation;

/// <summary>
/// Spec 002: a person needs first and last name, a company (RUC 6/9) needs a business name, the
/// email is optional, and the identification follows <see cref="EcuadorIdentification"/> — the
/// same library the Blazor screens use (FR-009).
/// </summary>
public sealed class CreateCustomerCommandValidator : AbstractValidator<Commands.CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        When(x => string.IsNullOrWhiteSpace(x.BusinessName), () =>
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        });

        RuleFor(x => x.BusinessName).MaximumLength(300);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(255).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(30);

        RuleFor(x => x.IdentificationNumber).Custom((_, context) =>
        {
            var item = context.InstanceToValidate;
            foreach (var (property, message) in IdentificationRules.Failures(
                         item.IdentificationType, item.IdentificationNumber, item.BusinessName))
            {
                context.AddFailure(property, message);
            }
        });

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

        When(x => string.IsNullOrWhiteSpace(x.BusinessName), () =>
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        });

        RuleFor(x => x.BusinessName).MaximumLength(300);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Status).IsInEnum();

        RuleFor(x => x.IdentificationNumber).Custom((_, context) =>
        {
            var item = context.InstanceToValidate;
            foreach (var (property, message) in IdentificationRules.Failures(
                         item.IdentificationType, item.IdentificationNumber, item.BusinessName))
            {
                context.AddFailure(property, message);
            }
        });
    }
}

/// <summary>Identification checks shared by create and update.</summary>
public static class IdentificationRules
{
    public static IEnumerable<(string Property, string Message)> Failures(
        IdentificationType? type,
        string? number,
        string? businessName)
    {
        var value = number?.Trim();
        var hasNumber = !string.IsNullOrEmpty(value);

        if (type is null && !hasNumber)
        {
            yield break;
        }

        if (type is null)
        {
            yield return ("IdentificationType", "Indica el tipo de identificación.");
            yield break;
        }

        if (!hasNumber)
        {
            yield return ("IdentificationNumber", "Indica el número de identificación.");
            yield break;
        }

        var error = EcuadorIdentification.Validate(type.Value, value);
        if (error is not null)
        {
            yield return ("IdentificationNumber", error);
            yield break;
        }

        if (type == IdentificationType.Ruc
            && EcuadorIdentification.IsCompanyRuc(value)
            && string.IsNullOrWhiteSpace(businessName))
        {
            yield return ("BusinessName", "Un RUC de sociedad requiere razón social.");
        }
    }
}
