namespace SagaSales.Notification.Domain;

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid? CustomerId { get; set; }

    public NotificationType Type { get; set; }

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Status = NotificationStatus.Failed;
        Error = error;
    }
}

public class NotificationTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Variables { get; set; } = "{}";
}
