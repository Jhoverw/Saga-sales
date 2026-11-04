namespace SagaSales.Shared.DTOs;

public class SalesSummaryDto
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public decimal TotalRevenue { get; set; }

    public int OrderCount { get; set; }

    public int PaymentSuccessCount { get; set; }

    public IReadOnlyList<TopProductDto> TopProducts { get; set; } = [];
}

public class TopProductDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int UnitsSold { get; set; }

    public decimal Revenue { get; set; }
}

/// <summary>One point of the day-by-day series.</summary>
public class DailyPointDto
{
    public DateOnly Date { get; set; }

    public decimal Revenue { get; set; }

    public int Orders { get; set; }
}

public class NotificationDto
{
    public Guid Id { get; set; }

    public Enums.NotificationType Type { get; set; }

    public Enums.NotificationStatus Status { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public string? Error { get; set; }
}

public class SendNotificationRequest
{
    public Enums.NotificationType Type { get; set; } = Enums.NotificationType.Email;

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}
