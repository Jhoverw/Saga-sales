namespace SagaSales.BuildingBlocks.Security;

using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SagaSales.BuildingBlocks.Extensions;
using SagaSales.Shared.Constants;

/// <summary>
/// Resolves the current tenant from header, query, subdomain or JWT claim, and stores it
/// on the request so <see cref="ICurrentUser"/> can read it.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = ResolveTenantId(context);

        if (tenantId.HasValue)
        {
            context.Items[TenantContextKeys.ItemKey] = tenantId.Value;

            if (context.User.Identity?.IsAuthenticated == true &&
                context.User.FindFirst(SagaSalesConstants.Claims.TenantId) is null &&
                context.User.Identity is ClaimsIdentity identity)
            {
                identity.AddClaim(new Claim(SagaSalesConstants.Claims.TenantId, tenantId.Value.ToString()));
            }

            logger.LogDebug("Resolved tenant {TenantId}", tenantId.Value);
        }

        await next(context);
    }

    private static Guid? ResolveTenantId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(SagaSalesConstants.Headers.TenantId, out var headerValue) &&
            Guid.TryParse(headerValue.FirstOrDefault(), out var headerTenantId))
        {
            return headerTenantId;
        }

        if (context.Request.Query.TryGetValue(SagaSalesConstants.Headers.TenantId, out var queryValue) &&
            Guid.TryParse(queryValue.FirstOrDefault(), out var queryTenantId))
        {
            return queryTenantId;
        }

        var host = context.Request.Host.Host;
        var firstLabel = host.Contains('.') ? host.Split('.')[0] : null;
        if (firstLabel is not null && Guid.TryParse(firstLabel, out var subdomainTenantId))
        {
            return subdomainTenantId;
        }

        var tenantClaim = context.User?.FindFirst(SagaSalesConstants.Claims.TenantId);
        return tenantClaim is not null && Guid.TryParse(tenantClaim.Value, out var claimTenantId)
            ? claimTenantId
            : null;
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
        => builder.UseMiddleware<TenantResolutionMiddleware>();
}
