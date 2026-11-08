using SagaSales.Shared;
using SagaSales.Shared.Contracts;
using SagaSales.BuildingBlocks.Security;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;

namespace SagaSales.Notification.Queries;

public record GetNotificationsQuery(int PageNumber = 1, int PageSize = 20) : IQuery<PagedResult<NotificationDto>>;

public sealed class GetNotificationsQueryHandler(IConfiguration config, ICurrentUser currentUser) : IQueryHandler<GetNotificationsQuery, PagedResult<NotificationDto>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();
        var connectionString = config.GetConnectionString("DefaultConnection")!;
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 20 : request.PageSize;

        using (var conn = new SqlConnection(connectionString))
        {
            string countSql = "SELECT COUNT(*) FROM notification_logs WHERE tenant_id = @tenantId";
            int totalCount = await conn.ExecuteScalarAsync<int>(countSql, new { tenantId });

            string dataSql = @"
                SELECT id              AS Id,
                       customer_id     AS CustomerId,
                       notification_type AS Type,
                       channel         AS Channel,
                       subject         AS Subject,
                       body            AS Body,
                       status          AS Status,
                       sent_at         AS SentAt,
                       created_at      AS CreatedAt
                FROM notification_logs
                WHERE tenant_id = @tenantId
                ORDER BY created_at DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            var items = await conn.QueryAsync<NotificationDto>(dataSql, new { tenantId, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize });

            var result = new PagedResult<NotificationDto>(items.ToList(), pageNumber, pageSize, totalCount);
            return Result<PagedResult<NotificationDto>>.Success(result);
        }
    }
}

public record NotificationDto(Guid Id, Guid? CustomerId, int Type, string Channel, string Subject, string Body, int Status, DateTime? SentAt, DateTime CreatedAt);
