namespace SagaSales.BuildingBlocks.Extensions;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using SagaSales.Shared.Constants;

/// <summary>
/// Ambient access to the caller's tenant and identity, resolved from the request.
/// Replaces the non-existent <c>HttpContext.Current</c> of ASP.NET Framework.
/// </summary>
public interface ICurrentUser
{
    Guid? TenantId { get; }

    Guid? UserId { get; }

    string? Email { get; }

    /// <summary>Tenant of the caller, or throws when the request carries none.</summary>
    Guid RequireTenantId();

    /// <summary>User of the caller, or throws when the request is anonymous.</summary>
    Guid RequireUserId();
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? TenantId
    {
        get
        {
            var context = accessor.HttpContext;
            if (context is null)
            {
                return null;
            }

            if (context.Items.TryGetValue(TenantContextKeys.ItemKey, out var item) && item is Guid tenantId)
            {
                return tenantId;
            }

            var claim = context.User?.FindFirst(SagaSalesConstants.Claims.TenantId);
            return claim is not null && Guid.TryParse(claim.Value, out var fromClaim) ? fromClaim : null;
        }
    }

    public Guid? UserId
    {
        get
        {
            var context = accessor.HttpContext;
            if (context?.User?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var value = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst(SagaSalesConstants.Claims.UserId)?.Value;

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => accessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
        ?? accessor.HttpContext?.User?.FindFirst(SagaSalesConstants.Claims.Email)?.Value;

    public Guid RequireTenantId() => TenantId
        ?? throw new SagaSales.Shared.Exceptions.TenantNotFoundException(Guid.Empty);

    public Guid RequireUserId() => UserId
        ?? throw new SagaSales.Shared.Exceptions.UnauthorizedException("The request has no authenticated user.");
}

/// <summary>Keys used to stash per-request values in <see cref="HttpContext.Items"/>.</summary>
public static class TenantContextKeys
{
    public const string ItemKey = "TenantId";
}
