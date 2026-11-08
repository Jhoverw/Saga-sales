using SagaSales.Shared;
using SagaSales.Shared.Contracts;
using SagaSales.BuildingBlocks.EventBus;
using Microsoft.Extensions.Logging;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using MassTransit;

namespace SagaSales.Notification.Services;

public interface INotificationService
{
    Task SendAsync(Guid tenantId, Guid? customerId, string subject, string body, int type, string channel, CancellationToken ct = default);
}

public class NotificationService(IConfiguration config, ILogger<NotificationService> logger) : INotificationService
{
    private readonly string _connectionString = config.GetConnectionString("DefaultConnection")!;

    public async Task SendAsync(Guid tenantId, Guid? customerId, string subject, string body, int type, string channel, CancellationToken ct = default)
    {
        using var conn = new SqlConnection(_connectionString);
        
        const string sql = @"
            INSERT INTO notification_logs (tenant_id, customer_id, notification_type, channel, subject, body, status, sent_at, created_at)
            VALUES (@tenantId, @customerId, @type, @channel, @subject, @body, 2, GETDATE(), GETDATE());";

        await conn.ExecuteAsync(sql, new { tenantId, customerId, type, channel, subject, body });
        logger.LogInformation("Notification sent to {CustomerId} via {Channel}", customerId, channel);
    }
}

public class OrderConfirmationConsumer(INotificationService notificationService, ILogger<OrderConfirmationConsumer> logger) : IConsumer<OrderPlacedEvent>
{
    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var e = context.Message;
        logger.LogInformation("Processing order confirmation for {OrderId}", e.OrderId);

        await notificationService.SendAsync(
            e.TenantId, 
            e.CustomerId, 
            $"Order Confirmed: {e.OrderId}", 
            $"Your order has been placed. Total: {e.TotalAmount:C}", 
            1, "email");
    }
}
