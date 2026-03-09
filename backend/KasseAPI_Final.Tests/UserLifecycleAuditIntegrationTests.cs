using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Integration tests: immutable audit event persistence for user lifecycle (RKSV/BAO traceability).
/// </summary>
public class UserLifecycleAuditIntegrationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"UserLifecycleAudit_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task LogUserLifecycleAsync_PersistsDeactivateEventToDb()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var auditService = new AuditLogService(context, new Mock<ILogger<AuditLogService>>().Object, httpContextAccessor.Object);

        await auditService.LogUserLifecycleAsync(
            AuditLogActions.USER_DEACTIVATE,
            "admin-1", "Admin", "u1",
            "Ausscheiden zum 31.03.2025", null, AuditLogStatus.Success,
            "User deactivated: u. Reason: Ausscheiden zum 31.03.2025");

        var count = await context.AuditLogs.CountAsync(a => a.Action == AuditLogActions.USER_DEACTIVATE && a.EntityName == "u1");
        Assert.Equal(1, count);
        var log = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == AuditLogActions.USER_DEACTIVATE && a.EntityName == "u1");
        Assert.NotNull(log);
        Assert.Contains("Ausscheiden", log.Notes ?? "");
    }

    /// <summary>Smoke: deactivate + reactivate lifecycle produces two immutable audit records.</summary>
    [Fact]
    public async Task LogUserLifecycle_DeactivateAndReactivate_PersistsTwoEvents()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var auditService = new AuditLogService(context, new Mock<ILogger<AuditLogService>>().Object, httpContextAccessor.Object);

        await auditService.LogUserLifecycleAsync(AuditLogActions.USER_DEACTIVATE, "admin-1", "Admin", "u1", "Urlaub", null, AuditLogStatus.Success, "Deactivated");
        await auditService.LogUserLifecycleAsync(AuditLogActions.USER_REACTIVATE, "admin-1", "Admin", "u1", null, null, AuditLogStatus.Success, "Reactivated");

        var deactivateLogs = await context.AuditLogs.Where(a => a.Action == AuditLogActions.USER_DEACTIVATE && a.EntityName == "u1").ToListAsync();
        var reactivateLogs = await context.AuditLogs.Where(a => a.Action == AuditLogActions.USER_REACTIVATE && a.EntityName == "u1").ToListAsync();
        Assert.Single(deactivateLogs);
        Assert.Single(reactivateLogs);
    }
}
