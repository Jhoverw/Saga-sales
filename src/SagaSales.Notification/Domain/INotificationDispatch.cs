namespace SagaSales.Notification.Domain;

/// <summary>Contract used by the dispatcher so infrastructure depends on the domain, not the reverse.</summary>
public interface INotificationDispatch
{
    Task<NotificationLog> DispatchAsync(
        Guid tenantId,
        Guid? customerId,
        NotificationType type,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken);
}
