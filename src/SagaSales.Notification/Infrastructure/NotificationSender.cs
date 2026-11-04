namespace SagaSales.Notification.Infrastructure;

/// <summary>
/// Delivery abstraction. Phase 2 plugs MailKit (SMTP) and Twilio (SMS) in behind this
/// interface; the previous code referenced packages that were never installed.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(
        NotificationType type,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken);
}

/// <summary>
/// Logs instead of delivering. It is explicit about not sending anything, so nothing
/// silently looks delivered in a real environment.
/// </summary>
public sealed class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(
        NotificationType type,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "No delivery provider configured: {Type} to {Recipient} was logged, not sent. Subject={Subject}",
            type,
            recipient,
            subject);

        logger.LogInformation("Notification body: {Body}", body);
        return Task.CompletedTask;
    }
}
