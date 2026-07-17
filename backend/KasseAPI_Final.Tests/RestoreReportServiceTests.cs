using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreReportServiceTests
{
    [Fact]
    public async Task GenerateRestoreReportAsync_returns_null_when_missing()
    {
        var (sut, _) = CreateSut($"rr_missing_{Guid.NewGuid():N}");
        var report = await sut.GenerateRestoreReportAsync(Guid.NewGuid());
        Assert.Null(report);
    }

    [Fact]
    public async Task GenerateRestoreReportAsync_marks_pending_as_not_compliant()
    {
        var (sut, db) = CreateSut($"rr_pending_{Guid.NewGuid():N}");
        var backupId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        });
        db.ManualRestoreRequests.Add(new ManualRestoreRequest
        {
            Id = requestId,
            BackupRunId = backupId,
            Status = ManualRestoreRequestStatus.PendingApproval,
            TargetDatabaseName = "restore_validation_pending",
            ValidationOnly = true,
            RequestedAt = DateTime.UtcNow,
            RequestedByUserId = "requester"
        });
        await db.SaveChangesAsync();

        var report = await sut.GenerateRestoreReportAsync(requestId);

        Assert.NotNull(report);
        Assert.True(report!.ComplianceChecked);
        Assert.False(report.RksvCompliant);
        Assert.Contains("dual_superadmin_approval_pending", report.ComplianceFindings);
        Assert.Equal(backupId, report.BackupId);
        Assert.Equal("PendingApproval", report.Status);
    }

    [Fact]
    public async Task GenerateRestoreReportAsync_completed_drill_success_is_compliant()
    {
        var (sut, db) = CreateSut($"rr_ok_{Guid.NewGuid():N}");
        var tenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var backupId = Guid.NewGuid();
        var drillId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo Mandant",
            Slug = "demo",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            TenantId = tenantId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddHours(-20)
        });
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = drillId,
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            SourceBackupRunId = backupId,
            RequestedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            PgRestoreListLineCount = 42,
            FiscalSqlSkipped = false,
            FiscalSqlPassed = true,
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = true
        });
        db.ManualRestoreRequests.Add(new ManualRestoreRequest
        {
            Id = requestId,
            BackupRunId = backupId,
            Status = ManualRestoreRequestStatus.Completed,
            TargetDatabaseName = "restore_validation_ok",
            ValidationOnly = true,
            RequestedAt = DateTime.UtcNow.AddHours(-3),
            RequestedByUserId = "requester",
            ApprovedByUserId = "approver",
            ApprovedAt = DateTime.UtcNow.AddHours(-2),
            RestoreVerificationRunId = drillId,
            CorrelationId = "corr-report-1"
        });
        await db.SaveChangesAsync();

        var report = await sut.GenerateRestoreReportAsync(requestId);

        Assert.NotNull(report);
        Assert.True(report!.RksvCompliant);
        Assert.Equal(tenantId, report.TenantId);
        Assert.Equal("Demo Mandant", report.TenantName);
        Assert.Equal(42, report.TablesRestored);
        Assert.Null(report.RecordsRestored);
        Assert.Equal("Succeeded", report.DrillStatus);
        Assert.Contains("linked_drill_succeeded", report.ComplianceFindings);
        Assert.Contains("fiscal_sql_passed", report.ComplianceFindings);
        Assert.Equal(RestoreReportService.RksvComplianceNotes, report.RksvComplianceNotes);
    }

    [Fact]
    public async Task GenerateRestoreReportAsync_failed_drill_is_not_compliant()
    {
        var (sut, db) = CreateSut($"rr_fail_{Guid.NewGuid():N}");
        var backupId = Guid.NewGuid();
        var drillId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow
        });
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = drillId,
            Status = RestoreVerificationStatus.Failed,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            SourceBackupRunId = backupId,
            RequestedAt = DateTime.UtcNow,
            FiscalSqlPassed = false,
            FiscalSqlSkipped = false
        });
        db.ManualRestoreRequests.Add(new ManualRestoreRequest
        {
            Id = requestId,
            BackupRunId = backupId,
            Status = ManualRestoreRequestStatus.Failed,
            TargetDatabaseName = "restore_validation_fail",
            ValidationOnly = true,
            RequestedByUserId = "requester",
            ApprovedByUserId = "approver",
            ApprovedAt = DateTime.UtcNow,
            RestoreVerificationRunId = drillId
        });
        await db.SaveChangesAsync();

        var report = await sut.GenerateRestoreReportAsync(requestId);

        Assert.NotNull(report);
        Assert.False(report!.RksvCompliant);
        Assert.Contains("restore_failed", report.ComplianceFindings);
        Assert.Contains("fiscal_sql_failed", report.ComplianceFindings);
    }

    [Fact]
    public async Task GenerateRestoreReportAsync_rejects_non_validation_target()
    {
        var (sut, db) = CreateSut($"rr_prod_{Guid.NewGuid():N}");
        var backupId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow
        });
        db.ManualRestoreRequests.Add(new ManualRestoreRequest
        {
            Id = requestId,
            BackupRunId = backupId,
            Status = ManualRestoreRequestStatus.Completed,
            TargetDatabaseName = "regkasse_app",
            ValidationOnly = true,
            RequestedByUserId = "requester",
            ApprovedByUserId = "approver",
            ApprovedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var report = await sut.GenerateRestoreReportAsync(requestId);

        Assert.NotNull(report);
        Assert.False(report!.RksvCompliant);
        Assert.Contains("target_not_isolated_validation_database", report.ComplianceFindings);
    }

    private static (RestoreReportService Sut, AppDbContext Db) CreateSut(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=regkasse_app;Username=u;Password=p"
            })
            .Build();

        var guard = new ManualRestoreTargetDatabaseGuard(
            config,
            new OptionsMonitorStub(new ManualRestoreApprovalOptions()));

        return (new RestoreReportService(db, guard), db);
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<ManualRestoreApprovalOptions>
    {
        public OptionsMonitorStub(ManualRestoreApprovalOptions value) => CurrentValue = value;
        public ManualRestoreApprovalOptions CurrentValue { get; }
        public ManualRestoreApprovalOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ManualRestoreApprovalOptions, string?> listener) => null;
    }
}
