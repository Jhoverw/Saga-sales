namespace SagaSales.Notification.Data;

using Dapper;
using SagaSales.BuildingBlocks.Data;

public interface INotificationRepository
{
    Task<Guid> CreateNotificationAsync(
        Guid tenantId,
        Guid? customerId,
        string subject,
        string body,
        int type,
        string channel,
        CancellationToken cancellationToken);

    Task MarkSentAsync(Guid id, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationLogDto>> ListByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}

public record NotificationLogDto(
    Guid Id,
    Guid? CustomerId,
    string Subject,
    int Status,
    DateTime? SentAt,
    string? ErrorMessage,
    DateTime CreatedAt);

public sealed class NotificationRepository(IStoredProcedureExecutor executor) : INotificationRepository
{
    public async Task<Guid> CreateNotificationAsync(
        Guid tenantId,
        Guid? customerId,
        string subject,
        string body,
        int type,
        string channel,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("p_tenant_id", tenantId)
            .With("p_customer_id", customerId)
            .With("p_subject", subject)
            .With("p_body", body)
            .With("p_type", type)
            .With("p_channel", channel);

        var id = await executor.QueryProcSingleAsync<Guid>(
            "dbo.sp_notification_create", arguments, cancellationToken: cancellationToken);

        return id;
    }

    public Task MarkSentAsync(Guid id, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_notification_mark_sent",
            SqlArgs.New().With("p_id", id),
            cancellationToken: cancellationToken);

    public Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_notification_mark_failed",
            SqlArgs.New().With("p_id", id).With("p_error", error),
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<NotificationLogDto>> ListByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryProcAsync<NotificationLogDto>(
            "dbo.sp_notification_list",
            SqlArgs.New().With("p_tenant_id", tenantId),
            cancellationToken: cancellationToken);

        return rows.ToList();
    }
}
