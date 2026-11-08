namespace SagaSales.Notification.Commands;

using SagaSales.Notification.Queries;
using SagaSales.Shared;

public record SendNotificationCommand(
    NotificationType Type,
    string Recipient,
    string Subject,
    string Body) : ICommand<NotificationDto>;

public sealed class SendNotificationCommandHandler(
    Domain.INotificationDispatch dispatcher,
    ICurrentUser currentUser) : ICommandHandler<SendNotificationCommand, NotificationDto>
{
    public async Task<Result<NotificationDto>> Handle(
        SendNotificationCommand request,
        CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Recipient, nameof(request.Recipient));
        Guard.AgainstNullOrEmpty(request.Subject, nameof(request.Subject));

        var tenantId = currentUser.RequireTenantId();

        var notificationId = await dispatcher.DispatchAsync(
            tenantId,
            null,
            request.Type,
            request.Recipient.Trim(),
            request.Subject.Trim(),
            request.Body,
            cancellationToken);

        return Result<NotificationDto>.Success(new NotificationDto(
            notificationId,
            null,
            (int)request.Type,
            "Email", // Default channel
            request.Subject.Trim(),
            request.Body,
            1, // Pending
            null,
            DateTime.UtcNow));
    }
}
