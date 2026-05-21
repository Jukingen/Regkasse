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
/// Regression: PostgreSQL 22001 (value too long) on audit varchar columns — oversized JSON must be truncated before SaveChanges.
/// </summary>
public sealed class AuditLogServiceLogSystemOperationBoundsTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AuditBounds_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task LogSystemOperationAsync_truncates_oversized_request_response_json_to_4000_chars()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var actorResolver = new Mock<IActorDisplayNameResolver>();
        var retentionOptions = new Mock<IOptions<AuditRetentionOptions>>();
        retentionOptions.Setup(x => x.Value).Returns(new AuditRetentionOptions());

        var auditService = new AuditLogService(
            context,
            new Mock<ILogger<AuditLogService>>().Object,
            httpContextAccessor.Object,
            new NullCurrentTenantAccessor(),
            actorResolver.Object,
            retentionOptions.Object);

        var huge = new string('x', 12_000);
        await auditService.LogSystemOperationAsync(
            action: "BACKUP_TEST",
            entityType: "BackupRun",
            userId: "u1",
            userRole: "SuperAdmin",
            description: "test",
            status: AuditLogStatus.Success,
            requestData: new { payload = huge },
            responseData: new { echo = huge },
            correlationIdOverride: null);

        var row = await context.AuditLogs.AsNoTracking().SingleAsync();
        Assert.NotNull(row.RequestData);
        Assert.NotNull(row.ResponseData);
        Assert.True(row.RequestData!.Length <= AuditLogPersistenceSanitizer.JsonPayloadMaxLength);
        Assert.True(row.ResponseData!.Length <= AuditLogPersistenceSanitizer.JsonPayloadMaxLength);
    }
}
