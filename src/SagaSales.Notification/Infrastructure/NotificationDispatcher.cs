namespace SagaSales.Notification.Infrastructure;

/// <summary>
/// Single place that delivers a notification and records the attempt, shared by the API
/// endpoints and the event consumers.
/// </summary>
public sealed class NotificationDispatcher(
    INotificationRepository repository,
    INotificationSender sender) : Domain.INotificationDispatch
{
    public async Task<Guid> DispatchAsync(
        Guid tenantId,
        Guid? customerId,
        NotificationType type,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var channel = type == NotificationType.SMS ? "sms" : "email";

        // 1. Log the attempt with its real content, so the list shows what was sent.
        var id = await repository.CreateNotificationAsync(
            tenantId,
            customerId,
            subject,
            body,
            (int)type,
            channel,
            cancellationToken);

        // 2. Deliver. A failure must never break the order (FR-023); the attempt is already logged.
        try
        {
            await sender.SendAsync(type, recipient, subject, body, cancellationToken);
            await repository.MarkSentAsync(id, cancellationToken);
        }
        catch (Exception)
        {
            // Best effort: the send failure is visible because the log stays in Pending.
        }

        return id;
    }
}

