using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ImpersonationAuditLogIntegrationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ImpersonationAudit_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static AuditLogService CreateAuditService(AppDbContext context, ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user != null)
        {
            httpContext.User = user;
        }

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var actorResolver = new Mock<IActorDisplayNameResolver>();
        actorResolver.Setup(x => x.ResolveAsync(It.IsAny<IList<string>>()))
            .ReturnsAsync(new Dictionary<string, string>());

        var retentionOptions = new Mock<IOptions<AuditRetentionOptions>>();
        retentionOptions.Setup(x => x.Value).Returns(new AuditRetentionOptions());

        return new AuditLogService(
            context,
            new Mock<ILogger<AuditLogService>>().Object,
            httpContextAccessor.Object,
            new NullCurrentTenantAccessor(),
            actorResolver.Object,
            retentionOptions.Object);
    }

    [Fact]
    public async Task LogSystemOperationAsync_UnderImpersonationJwt_PersistsImpersonationColumns()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "super-1"),
            new Claim("tenant_impersonation", "true"),
            new Claim(ScopeCheckService.TenantIdClaim, tenantId.ToString("D")),
        ],
        authenticationType: "Test"));

        var auditService = CreateAuditService(context, principal);

        await auditService.LogSystemOperationAsync(
            "TEST_ACTION",
            AuditLogEntityTypes.SYSTEM_CONFIG,
            "super-1",
            Roles.SuperAdmin);

        var row = await context.AuditLogs.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal("super-1", row.ImpersonatedBy);
        Assert.Equal(tenantId, row.ImpersonatedTenantId);
    }

    [Fact]
    public async Task LogImpersonationSessionStartedAsync_PersistsExplicitSnapshot()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var auditService = CreateAuditService(context);

        await auditService.LogImpersonationSessionStartedAsync(
            "super-2",
            Roles.SuperAdmin,
            tenantId,
            "dev");

        var row = await context.AuditLogs.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(AuditLogActions.TENANT_IMPERSONATION_STARTED, row.Action);
        Assert.Equal("super-2", row.ImpersonatedBy);
        Assert.Equal(tenantId, row.ImpersonatedTenantId);
    }
}
