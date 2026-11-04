namespace SagaSales.Notification.Infrastructure;

/// <summary>
/// Single place that delivers a notification and records the attempt, shared by the API
/// endpoints and the event consumers.
/// </summary>
public sealed class NotificationDispatcher(
    NotificationDbContext dbContext,
    INotificationSender sender) : Domain.INotificationDispatch
{
    public async Task<Domain.NotificationLog> DispatchAsync(
        Guid tenantId,
        Guid? customerId,
        NotificationType type,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var log = new Domain.NotificationLog
        {
            TenantId = tenantId,
            CustomerId = customerId,
            Type = type,
            Recipient = recipient,
            Subject = subject,
            Body = body,
        };

        try
        {
            await sender.SendAsync(type, recipient, subject, body, cancellationToken);
            log.MarkSent();
        }
        catch (Exception ex)
        {
            log.MarkFailed(ex.Message);
        }

        dbContext.NotificationLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);

        return log;
    }
}
