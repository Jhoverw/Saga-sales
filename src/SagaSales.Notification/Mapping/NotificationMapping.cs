namespace SagaSales.Notification.Mapping;

using SagaSales.Notification.Domain;

public static class NotificationMapping
{
    public static NotificationDto ToDto(this NotificationLog log) => new()
    {
        Id = log.Id,
        Type = log.Type,
        Status = log.Status,
        Recipient = log.Recipient,
        Subject = log.Subject,
        Body = log.Body,
        CreatedAt = log.CreatedAt,
        SentAt = log.SentAt,
        Error = log.Error,
    };
}
