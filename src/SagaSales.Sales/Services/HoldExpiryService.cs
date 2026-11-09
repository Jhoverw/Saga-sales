namespace SagaSales.Sales.Services;

using System.Security.Claims;
using SagaSales.Sales.Commands;

/// <summary>
/// Spec 008, FR-007: every few minutes cancels the pedidos whose payment was not verified in time, so their
/// stock goes back on sale. There is no user here, so each cancellation runs as the store's "system" user: a
/// short-lived token with the store's tenant, the same one the Catalog call forwards to release the stock.
/// </summary>
public sealed class HoldExpiryService(
    IServiceScopeFactory scopes,
    ILogger<HoldExpiryService> logger) : BackgroundService
{
    /// <summary>Fixed id of the system user that signs automatic cancellations.</summary>
    public static readonly Guid SystemUserId = new("00000000-0000-0000-0000-00000000a5e0");

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the service finish starting before the first sweep.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Hold expiry sweep failed; retrying in {Minutes} minutes", Interval.TotalMinutes);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<(Guid TenantId, Guid OrderId)> due;
        using (var scope = scopes.CreateScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<IOrderRepository>().HoldsDueAsync(100, cancellationToken);
        }

        var cancelled = 0;
        foreach (var (tenantId, orderId) in due)
        {
            using var scope = scopes.CreateScope();
            var services = scope.ServiceProvider;
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = SystemContext(services, tenantId);

            try
            {
                var mediator = services.GetRequiredService<IMediator>();
                await mediator.Send(new CancelOrderCommand(orderId), cancellationToken);
                await services.GetRequiredService<IOrderRepository>().MarkExpiredAsync(orderId, cancellationToken);
                cancelled++;
                logger.LogInformation("Pedido {OrderId} of tenant {TenantId} expired: stock released", orderId, tenantId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Pedido {OrderId} of tenant {TenantId} could not be expired", orderId, tenantId);
            }
            finally
            {
                accessor.HttpContext = null;
            }
        }

        return cancelled;
    }

    private static DefaultHttpContext SystemContext(IServiceProvider services, Guid tenantId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, SystemUserId.ToString()),
            new Claim(ClaimTypes.Name, "Sistema (vencimiento de pedidos)"),
            new Claim(ClaimTypes.Role, SagaSalesRoles.Cashier),
            new Claim(SagaSalesConstants.Claims.TenantId, tenantId.ToString()),
        };

        var token = services.GetRequiredService<IJwtTokenService>().GenerateAccessToken(claims);

        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "system")),
        };
        context.Items[TenantContextKeys.ItemKey] = tenantId;
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context;
    }
}
