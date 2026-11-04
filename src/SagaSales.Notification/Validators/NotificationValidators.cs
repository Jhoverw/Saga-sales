namespace SagaSales.Notification.Validators;

using FluentValidation;

public sealed class SendNotificationCommandValidator : AbstractValidator<Commands.SendNotificationCommand>
{
    public SendNotificationCommandValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Recipient).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Body).NotEmpty();
    }
}
