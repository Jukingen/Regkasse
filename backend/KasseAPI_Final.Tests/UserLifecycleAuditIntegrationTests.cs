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

/// <summary>
/// Integration tests: immutable audit event persistence for user lifecycle (RKSV/BAO traceability).
/// Reads use <c>IgnoreQueryFilters</c> because harness uses null ambient tenant while writes stamp
/// <see cref="LegacyDefaultTenantIds.Primary"/> (fail-closed EF filters would otherwise hide rows).
/// </summary>
public class UserLifecycleAuditIntegrationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"UserLifecycleAudit_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static AuditLogService CreateAuditService(AppDbContext context)
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var actorResolver = new Mock<IActorDisplayNameResolver>();
        actorResolver.Setup(x => x.ResolveAsync(It.IsAny<IList<string>>())).ReturnsAsync(new Dictionary<string, string>());
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

    private static IQueryable<AuditLog> AuditLogsUnfiltered(AppDbContext context) =>
        context.AuditLogs.IgnoreQueryFilters().AsNoTracking();

    [Fact]
    public async Task LogUserLifecycleAsync_PersistsDeactivateEventToDb()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var auditService = CreateAuditService(context);

        await auditService.LogUserLifecycleAsync(
            AuditLogActions.USER_DEACTIVATE,
            "admin-1", "SuperAdmin", "u1",
            "Ausscheiden zum 31.03.2025", null, AuditLogStatus.Success,
            "User deactivated: u. Reason: Ausscheiden zum 31.03.2025");

        var count = await AuditLogsUnfiltered(context)
            .CountAsync(a => a.Action == AuditLogActions.USER_DEACTIVATE && a.EntityName == "u1");
        Assert.Equal(1, count);
        var log = await AuditLogsUnfiltered(context)
            .FirstOrDefaultAsync(a => a.Action == AuditLogActions.USER_DEACTIVATE && a.EntityName == "u1");
        Assert.NotNull(log);
        Assert.Contains("Ausscheiden", log.Notes ?? "");
        Assert.Equal("admin-1", log.UserId);
        Assert.NotEqual(default, log.Timestamp);
        Assert.Equal(LegacyDefaultTenantIds.Primary, log.TenantId);
    }

    /// <summary>Smoke: deactivate + reactivate lifecycle produces two immutable audit records.</summary>
    [Fact]
    public async Task LogUserLifecycle_DeactivateAndReactivate_PersistsTwoEvents()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var auditService = CreateAuditService(context);

        await auditService.LogUserLifecycleAsync(AuditLogActions.USER_DEACTIVATE, "admin-1", "SuperAdmin", "u1", "Urlaub", null, AuditLogStatus.Success, "Deactivated");
        await auditService.LogUserLifecycleAsync(AuditLogActions.USER_REACTIVATE, "admin-1", "SuperAdmin", "u1", null, null, AuditLogStatus.Success, "Reactivated");

        var deactivateLogs = await AuditLogsUnfiltered(context)
            .Where(a => a.Action == AuditLogActions.USER_DEACTIVATE && a.EntityName == "u1").ToListAsync();
        var reactivateLogs = await AuditLogsUnfiltered(context)
            .Where(a => a.Action == AuditLogActions.USER_REACTIVATE && a.EntityName == "u1").ToListAsync();
        Assert.Single(deactivateLogs);
        Assert.Single(reactivateLogs);
    }

    [Fact]
    public async Task LogUserLifecycleAsync_UsesDefaultTenant_WhenAmbientTenantIsMissing()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        context.Tenants.Add(new Tenant
        {
            Id = LegacyDefaultTenantIds.Primary,
            Name = "Default",
            Slug = LegacyDefaultTenantIds.PrimarySlug,
        });
        await context.SaveChangesAsync();

        var auditService = CreateAuditService(context);

        await auditService.LogUserLifecycleAsync(
            AuditEventType.PasswordResetForced,
            "admin-1",
            "SuperAdmin",
            "user-1",
            "Temporary password generated by Super Admin.");

        var log = await AuditLogsUnfiltered(context).SingleAsync();
        Assert.Equal(LegacyDefaultTenantIds.Primary, log.TenantId);
    }

    [Fact]
    public async Task LogUserLifecycleAsync_PermissionOverrides_UsesDedicatedActionString()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var auditService = CreateAuditService(context);

        await auditService.LogUserLifecycleAsync(
            AuditEventType.UserPermissionOverridesChanged,
            "admin-1",
            "SuperAdmin",
            "user-1",
            description: "Permission overrides updated");

        var log = await AuditLogsUnfiltered(context).SingleAsync();
        Assert.Equal(AuditEventType.UserPermissionOverridesChanged, log.ActionType);
        Assert.Equal(AuditLogActions.USER_PERMISSION_OVERRIDES_CHANGED, log.Action);
        Assert.False(string.IsNullOrWhiteSpace(log.UserId));
        Assert.NotEqual(default, log.Timestamp);
    }

    [Fact]
    public async Task LogUserLifecycleAsync_LoginFailed_UsesDistinctActionString()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var auditService = CreateAuditService(context);

        await auditService.LogUserLifecycleAsync(
            AuditEventType.LoginFailed,
            "anonymous",
            Roles.FallbackUnknown,
            "anonymous",
            status: AuditLogStatus.Failed,
            description: "Login failed (Invalid password)");

        var log = await AuditLogsUnfiltered(context).SingleAsync();
        Assert.Equal(AuditEventType.LoginFailed, log.ActionType);
        Assert.Equal(AuditLogActions.USER_LOGIN_FAILED, log.Action);
        Assert.Equal(AuditLogStatus.Failed, log.Status);
    }

    [Fact]
    public async Task LogEntityChangeAsync_CategoryUpdated_SetsActionType()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var auditService = CreateAuditService(context);

        var categoryId = Guid.NewGuid();
        await auditService.LogEntityChangeAsync(
            AuditLogActions.CATEGORY_UPDATED,
            AuditLogEntityTypes.CATEGORY,
            categoryId,
            "admin-1",
            "Manager",
            oldValues: new { Name = "Alt" },
            newValues: new { Name = "Neu" });

        var log = await AuditLogsUnfiltered(context).SingleAsync();
        Assert.Equal(AuditEventType.CategoryUpdated, log.ActionType);
        Assert.Equal(categoryId, log.EntityId);
        Assert.Equal(LegacyDefaultTenantIds.Primary, log.TenantId);
    }
}
