using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminRestoreVerificationControllerTests
{
    private sealed class CapturedPayload
    {
        public object? Value { get; private set; }

        public bool Keep(object? value)
        {
            Value = value;
            return true;
        }
    }

    [Fact]
    public async Task Pdf_returns_404_for_a_foreign_tenant_and_pdf_for_the_owner()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = await CreateDbAsync();
        var own = await SeedAsync(db, tenantA, RestoreVerificationStatus.Succeeded, failureCode: null);
        var foreign = await SeedAsync(db, tenantB, RestoreVerificationStatus.Succeeded, failureCode: null);
        var audit = TicketAuditTestDoubles.Loose();
        var controller = CreateController(db, audit, tenantA, "actor-a");

        var missing = await controller.DownloadPdf(foreign, CancellationToken.None);
        Assert.IsType<NotFoundResult>(missing);

        var ok = await controller.DownloadPdf(own, CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(ok);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal($"restore-drill-{own}.pdf", file.FileDownloadName);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(file.FileContents, 0, 5), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pdf_writes_RestoreDrillPdfDownloaded_once_without_secrets()
    {
        var tenantA = Guid.NewGuid();
        await using var db = await CreateDbAsync();
        var id = await SeedAsync(db, tenantA, RestoreVerificationStatus.Failed, failureCode: "FAILED", detail: "password=hunter2");
        var captured = new CapturedPayload();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.Is<object?>(value => captured.Keep(value)), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var controller = CreateController(db, audit, tenantA, "actor-a");
        var result = await controller.DownloadPdf(id, CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
        audit.Verify(a => a.LogSystemOperationAsync(
            "RESTORE_DRILL_PDF_DOWNLOADED",
            "RestoreVerificationRun",
            "actor-a",
            Roles.SuperAdmin,
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<AuditLogStatus>(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<ImpersonationAuditContext.Snapshot?>(),
            AuditEventType.RestoreDrillPdfDownloaded,
            id,
            tenantA,
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.Once);

        Assert.NotNull(captured.Value);
        var names = captured.Value!.GetType().GetProperties().Select(property => property.Name).ToArray();
        Assert.Equal(new[] { "tenantId", "drillId", "actorUserId" }, names);
        var dumped = string.Join(' ', captured.Value.GetType().GetProperties().Select(property => property.GetValue(captured.Value)?.ToString()));
        Assert.DoesNotContain("hunter2", dumped, StringComparison.Ordinal);
        Assert.Equal(151, (int)AuditEventType.RestoreDrillPdfDownloaded);
    }

    [Fact]
    public async Task Pdf_rejects_a_cancelled_drill()
    {
        var tenantA = Guid.NewGuid();
        await using var db = await CreateDbAsync();
        var id = await SeedAsync(db, tenantA, RestoreVerificationStatus.Failed, failureCode: "CANCELLED");
        var controller = CreateController(db, TicketAuditTestDoubles.Loose(), tenantA, "actor-a");

        var result = await controller.DownloadPdf(id, CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var code = bad.Value!.GetType().GetProperty("code")!.GetValue(bad.Value) as string;
        Assert.Equal("DRILL_NOT_REPORTABLE", code);
    }

    [Fact]
    public void Pdf_requires_system_critical()
    {
        var method = typeof(AdminBackupDrillsController).GetMethod(nameof(AdminBackupDrillsController.DownloadPdf));
        var permission = Assert.Single(method!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true).Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.SystemCritical, permission.Permission);
    }

    private static AdminBackupDrillsController CreateController(
        AppDbContext db,
        Mock<IAuditLogService> audit,
        Guid tenantId,
        string actor)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        return new AdminBackupDrillsController(
            new RestoreVerificationRunQueryService(db),
            new RestoreDrillPdfRenderer(),
            audit.Object,
            accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, actor),
                            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                        ],
                        authenticationType: "test")),
                },
            },
        };
    }

    private static async Task<AppDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"drill-pdf-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<Guid> SeedAsync(
        AppDbContext db,
        Guid tenantId,
        RestoreVerificationStatus status,
        string? failureCode,
        string? detail = null)
    {
        var backupId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            Strategy = BackupStrategyKind.Tenant,
            TenantId = tenantId,
            RequestedAt = new DateTime(2026, 10, 20, 6, 0, 0, DateTimeKind.Utc),
        });
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = runId,
            Status = status,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            SourceBackupRunId = backupId,
            RequestedAt = new DateTime(2026, 10, 21, 6, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 10, 21, 6, 2, 0, DateTimeKind.Utc),
            DurationMs = 90_000,
            FailureCode = failureCode,
            FailureDetail = detail,
            PgRestoreListPassed = status == RestoreVerificationStatus.Succeeded,
        });
        await db.SaveChangesAsync();
        return runId;
    }
}
