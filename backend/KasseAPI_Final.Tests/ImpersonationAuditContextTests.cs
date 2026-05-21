using System.Security.Claims;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ImpersonationAuditContextTests
{
    [Fact]
    public void FromClaimsPrincipal_WhenImpersonationClaimSet_ReturnsActorAndTenant()
    {
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "super-admin-42"),
            new Claim("tenant_impersonation", "true"),
            new Claim(ScopeCheckService.TenantIdClaim, tenantId.ToString("D")),
        ],
        authenticationType: "Test"));

        var snapshot = ImpersonationAuditContext.FromClaimsPrincipal(principal);

        Assert.Equal("super-admin-42", snapshot.ImpersonatedBy);
        Assert.Equal(tenantId, snapshot.ImpersonatedTenantId);
    }

    [Fact]
    public void FromClaimsPrincipal_WithoutImpersonationClaim_ReturnsEmpty()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ScopeCheckService.TenantIdClaim, Guid.NewGuid().ToString("D")),
        ],
        authenticationType: "Test"));

        var snapshot = ImpersonationAuditContext.FromClaimsPrincipal(principal);

        Assert.False(snapshot.IsActive);
    }

    [Fact]
    public void ApplyTo_SetsAuditLogColumns()
    {
        var tenantId = Guid.NewGuid();
        var log = new AuditLog { Id = Guid.NewGuid() };
        ImpersonationAuditContext.ApplyTo(
            log,
            ImpersonationAuditContext.ForSessionStart("admin-1", tenantId));

        Assert.Equal("admin-1", log.ImpersonatedBy);
        Assert.Equal(tenantId, log.ImpersonatedTenantId);
    }
}
