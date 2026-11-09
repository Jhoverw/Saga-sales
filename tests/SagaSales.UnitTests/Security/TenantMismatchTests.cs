using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SagaSales.BuildingBlocks.Security;
using SagaSales.Shared.Constants;
using Xunit;

namespace SagaSales.UnitTests.Security;

/// <summary>Una sesión pertenece a una tienda: la cabecera X-Tenant-Id no puede elegir otra.</summary>
public class TenantMismatchTests
{
    private static readonly Guid Store = Guid.NewGuid();

    [Fact]
    public void Another_store_in_the_header_is_foreign_for_a_signed_in_user()
    {
        Assert.True(TenantResolutionMiddleware.IsForeignTenant(SignedIn(Store), Guid.NewGuid()));
    }

    [Fact]
    public void The_same_store_is_accepted()
    {
        Assert.False(TenantResolutionMiddleware.IsForeignTenant(SignedIn(Store), Store));
    }

    [Fact]
    public void Anonymous_requests_still_choose_the_store_by_header()
    {
        Assert.False(TenantResolutionMiddleware.IsForeignTenant(new DefaultHttpContext(), Guid.NewGuid()));
    }

    [Fact]
    public void No_requested_store_is_never_foreign()
    {
        Assert.False(TenantResolutionMiddleware.IsForeignTenant(SignedIn(Store), null));
    }

    private static DefaultHttpContext SignedIn(Guid tenantId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(SagaSalesConstants.Claims.TenantId, tenantId.ToString())],
            authenticationType: "Test")),
    };
}
