namespace SagaSales.BuildingBlocks.Extensions;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SagaSales.Shared.Constants;

/// <summary>
/// Convenience accessors over <see cref="HttpContext"/>. For injected access use
/// <see cref="ICurrentUser"/> instead: ASP.NET Core has no <c>HttpContext.Current</c>.
/// </summary>
public static class HttpContextExtensions
{
    public static Guid? GetTenantId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(TenantContextKeys.ItemKey, out var item) && item is Guid tenantId)
        {
            return tenantId;
        }

        if (context.Request.Headers.TryGetValue(SagaSalesConstants.Headers.TenantId, out var header) &&
            Guid.TryParse(header.FirstOrDefault(), out var headerTenantId))
        {
            return headerTenantId;
        }

        var claim = context.User?.FindFirst(SagaSalesConstants.Claims.TenantId);
        return claim is not null && Guid.TryParse(claim.Value, out var claimTenantId) ? claimTenantId : null;
    }

    public static Guid? GetUserId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var value = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst(SagaSalesConstants.Claims.UserId)?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static string? GetEmail(this HttpContext context)
        => context?.User?.FindFirst(ClaimTypes.Email)?.Value
           ?? context?.User?.FindFirst(SagaSalesConstants.Claims.Email)?.Value;
}
