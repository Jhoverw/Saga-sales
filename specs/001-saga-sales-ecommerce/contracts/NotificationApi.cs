// Notification contracts — dispatch and its log (E-07 · US-015/US-016).
// Declarative contract: documents the API, it is not compiled.
// Runtime DTOs live in src/SagaSales.Shared/DTOs/AnalyticsNotificationDtos.cs.
//
// ⚠️ STATUS: the endpoints are declared, but `db/notification/` has no scripts yet, so
// `notification_logs` does not exist and the service still uses EF Core. This contract describes the
// intended shape; it is not green until US7 lands.

namespace SagaSales.Contracts;

public enum NotificationType { Email = 1, SMS = 2, Push = 3, InApp = 4 }

public enum NotificationStatus { Pending = 1, Sent = 2, Failed = 3, Delivered = 4, Read = 5 }

public class NotificationDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public NotificationStatus Status { get; set; }
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? Error { get; set; }
}

public class SendNotificationRequest
{
    public NotificationType Type { get; set; } = NotificationType.Email;
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}

// HTTP Endpoints — all require a bearer token and a resolvable tenant (FR-004).
//
// POST /api/notifications              → 201 NotificationDto (records the attempt and dispatches)
// GET  /api/notifications?pageNumber=  → 200 PagedResult<NotificationDto> (state and error per attempt)
//
// Consumers (not HTTP): OrderPlacedConsumer sends the confirmation; HighValueOrderAlertConsumer
// alerts the operator above the configured threshold. A notification failure must NEVER break the
// order (FR-023): the error is recorded and the order flow continues.
//
// Errors: 400 VALIDATION_ERROR · 401 · 403
