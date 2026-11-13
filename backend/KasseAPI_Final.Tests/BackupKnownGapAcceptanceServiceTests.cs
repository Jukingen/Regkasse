using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Backup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupKnownGapAcceptanceServiceTests
{
    [Fact]
    public void Catalog_HasVersionAndTheFourIds()
    {
        var catalog = new BackupKnownGapsCatalog();

        Assert.Equal(1, catalog.Version);
        Assert.Equal(
            [
                BackupKnownGapStatusService.ExternalArchiveNotSet,
                BackupKnownGapStatusService.ScheduledDrillNotRun,
                BackupKnownGapStatusService.SystemLegalHoldNotConfigured,
                BackupKnownGapStatusService.DevOnlyFakeAdapter,
            ],
            catalog.Gaps.Select(gap => gap.Id).ToArray());
    }

    [Fact]
    public async Task Save_ThenGet_ReturnsAcceptedIds()
    {
        var tenantId = Guid.NewGuid();
        var acceptedAt = new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);
        await using var db = CreateDb();
        db.Tenants.Add(Tenant(tenantId, "dev"));
        await db.SaveChangesAsync();

        var service = Acceptance(db, []);
        var saved = await service.SaveAsync(
            tenantId,
            ["scheduled-drill-not-run", "external-archive-not-set"],
            "ops",
            acceptedAt);

        var loaded = await service.GetAsync(tenantId);

        Assert.Equal(["scheduled-drill-not-run", "external-archive-not-set"], saved.AcceptedGaps);
        Assert.Equal(saved.AcceptedGaps, loaded.AcceptedGaps);
        Assert.Equal("ops", loaded.AcceptedBy);
        Assert.Equal(acceptedAt, loaded.AcceptedAtUtc);

        var row = await db.TenantSettings.SingleAsync();
        Assert.Equal(tenantId, row.TenantId);
        Assert.Equal(BackupKnownGapAcceptanceService.SettingsKey, row.Key);
        Assert.Contains("scheduled-drill-not-run", row.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_UnknownId_Returns400_BackupKnownGapUnknown()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(Tenant(tenantId, "dev"));
        await db.SaveChangesAsync();

        var controller = Controller(db, tenantId, exists: true);
        var result = await controller.Save(
            new SaveBackupKnownGapsRequest
            {
                TenantId = tenantId,
                AcceptedGapIds = ["not-a-real-gap"],
                AcceptedBy = "ops",
            },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = JsonSerializer.Serialize(bad.Value);
        Assert.Contains("BACKUP_KNOWN_GAP_UNKNOWN", json, StringComparison.Ordinal);
        Assert.Contains("not-a-real-gap", json, StringComparison.Ordinal);
        Assert.Empty(await db.TenantSettings.ToListAsync());
    }

    [Fact]
    public async Task Save_MissingTenant_Returns404()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        var controller = Controller(db, tenantId, exists: false);

        var result = await controller.Get(tenantId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Save_WritesAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(Tenant(tenantId, "dev"));
        await db.SaveChangesAsync();

        var calls = new List<AuditCall>();
        var service = Acceptance(db, calls);
        await service.SaveAsync(
            tenantId,
            ["dev-only-fake-adapter"],
            "ops",
            new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

        var call = Assert.Single(calls);
        Assert.Equal(AuditEventType.BackupKnownGapsAccepted, call.Type);
        Assert.Equal(142, (int)AuditEventType.BackupKnownGapsAccepted);
        Assert.Equal(tenantId, call.TenantId);
    }

    [Fact]
    public async Task Save_DoesNotChangeRestoreProofTimestamps()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var proofAt = now.AddDays(-1);
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(Tenant(tenantId, "dev"));
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = proofAt.AddMinutes(-20),
            StartedAt = proofAt.AddMinutes(-20),
            CompletedAt = proofAt,
        });
        await db.SaveChangesAsync();

        var stats = Stats(db, now);
        var recoverability = Recoverability(db, now);
        var beforeStats = await stats.GetAsync();
        var beforeProof = await recoverability.GetAsync();

        await Acceptance(db, []).SaveAsync(
            tenantId,
            ["scheduled-drill-not-run"],
            "ops",
            new DateTimeOffset(now, TimeSpan.Zero));

        var afterStats = await stats.GetAsync();
        var afterProof = await recoverability.GetAsync();

        Assert.Equal(proofAt, beforeStats.LastSuccessfulRestoreDrillAtUtc);
        Assert.Equal(beforeStats.LastSuccessfulRestoreDrillAtUtc, afterStats.LastSuccessfulRestoreDrillAtUtc);
        Assert.Equal(proofAt, beforeProof.LastSuccessfulRestoreProofAt);
        Assert.Equal(beforeProof.LastSuccessfulRestoreProofAt, afterProof.LastSuccessfulRestoreProofAt);
    }

    [Fact]
    public async Task OpenGap_WhenAccepted_IsAmber_AndVerdictStaysFail()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(Tenant(tenantId, "dev"));
        SeedClosedChecks(db, now);
        await db.SaveChangesAsync();

        await Acceptance(db, []).SaveAsync(
            tenantId,
            [BackupKnownGapStatusService.ExternalArchiveNotSet],
            "ops",
            new DateTimeOffset(now, TimeSpan.Zero));

        var view = await Status(db, now, archiveRoot: null, allowsOverride: false).GetViewAsync(tenantId);
        var archive = Assert.Single(view.Gaps, gap => gap.Id == BackupKnownGapStatusService.ExternalArchiveNotSet);

        Assert.True(archive.Open);
        Assert.True(archive.Accepted);
        Assert.Equal("amber", archive.DisplayColor);
        Assert.Equal("ops", archive.AcceptedBy);
        Assert.False(view.AllowsGateOverride);
        Assert.Equal("fail", view.Verdict);
        Assert.DoesNotContain(view.Gaps, gap => gap.DisplayColor == "green" && gap.Id == archive.Id);
    }

    [Fact]
    public async Task OpenGap_WhenNotAccepted_StaysRed()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(Tenant(tenantId, "dev"));
        SeedClosedChecks(db, now);
        await db.SaveChangesAsync();

        var view = await Status(db, now, archiveRoot: null, allowsOverride: false).GetViewAsync(tenantId);
        var archive = Assert.Single(view.Gaps, gap => gap.Id == BackupKnownGapStatusService.ExternalArchiveNotSet);

        Assert.True(archive.Open);
        Assert.False(archive.Accepted);
        Assert.Equal("red", archive.DisplayColor);
        Assert.Equal("fail", view.Verdict);
    }

    [Fact]
    public async Task GateOverride_AllowsVerdictPass_ButColorStaysAmber()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(Tenant(tenantId, "dev"));
        SeedClosedChecks(db, now);
        await db.SaveChangesAsync();

        var ids = new BackupKnownGapsCatalog().Gaps.Select(gap => gap.Id).ToArray();
        await Acceptance(db, []).SaveAsync(
            tenantId,
            ids,
            "ops",
            new DateTimeOffset(now, TimeSpan.Zero));

        var view = await Status(db, now, archiveRoot: null, allowsOverride: true).GetViewAsync(tenantId);
        var archive = Assert.Single(view.Gaps, gap => gap.Id == BackupKnownGapStatusService.ExternalArchiveNotSet);

        Assert.Equal("amber", archive.DisplayColor);
        Assert.True(view.AllowsGateOverride);
        Assert.Equal("pass", view.Verdict);
    }

    private static void SeedClosedChecks(AppDbContext db, DateTime now)
    {
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = now.AddDays(-1),
            StartedAt = now.AddDays(-1),
            CompletedAt = now.AddDays(-1),
        });
        db.BackupRuns.Add(new BackupRun
        {
            Strategy = BackupStrategyKind.System,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = now.AddDays(-1),
            CompletedAt = now.AddDays(-1),
            LegalHold = true,
            LegalHoldUntilUtc = now.AddYears(7),
        });
    }

    private static BackupKnownGapStatusService Status(
        AppDbContext db,
        DateTime utcNow,
        string? archiveRoot,
        bool allowsOverride) =>
        new(
            new BackupKnownGapsCatalog(),
            Acceptance(db, []),
            new FixedMonitor<BackupOptions>(new BackupOptions
            {
                ExternalArchiveRoot = archiveRoot,
                ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            }),
            new FixedMonitor<BackupKnownGapsAcceptanceOptions>(new BackupKnownGapsAcceptanceOptions
            {
                AllowsGateOverride = allowsOverride,
            }),
            new TestHostEnvironment(Environments.Production),
            db,
            new FixedTime(utcNow));

    private static IBackupDashboardStatsService Stats(AppDbContext db, DateTime utcNow)
    {
        var readiness = new Mock<IBackupOperationalReadiness>();
        readiness.Setup(x => x.GetConfigurationHealth()).Returns(new BackupConfigurationHealthSnapshot
        {
            Level = BackupConfigurationHealthLevel.Healthy,
            EffectiveAdapterKind = BackupExecutionAdapterKind.PgDump,
        });
        readiness.Setup(x => x.GetArtifactPipelinePolicy()).Returns(new BackupArtifactPipelinePolicySnapshot
        {
            ExternalArchiveRootConfigured = true,
            EffectiveAdapterKind = BackupExecutionAdapterKind.PgDump,
        });
        return new BackupDashboardStatsService(
            db,
            new BackupRunQueryService(db),
            readiness.Object,
            new BackupStagingDiskMonitor(),
            new FixedMonitor<BackupOptions>(new BackupOptions()),
            new FixedTime(utcNow));
    }

    private static BackupRecoverabilitySummaryService Recoverability(AppDbContext db, DateTime utcNow)
    {
        var readiness = new Mock<IBackupOperationalReadiness>();
        readiness.Setup(x => x.GetConfigurationHealth()).Returns(new BackupConfigurationHealthSnapshot
        {
            Level = BackupConfigurationHealthLevel.Healthy,
            EffectiveAdapterKind = BackupExecutionAdapterKind.PgDump,
        });
        return new BackupRecoverabilitySummaryService(db, new FixedTime(utcNow), readiness.Object);
    }

    private static AdminBackupKnownGapsController Controller(AppDbContext db, Guid tenantId, bool exists)
    {
        var tenants = new Mock<IAdminTenantService>();
        tenants.Setup(service => service.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists ? Detail(tenantId) : null);
        return new AdminBackupKnownGapsController(
            Acceptance(db, []),
            new Mock<IBackupKnownGapStatusService>().Object,
            tenants.Object);
    }

    private static BackupKnownGapAcceptanceService Acceptance(AppDbContext db, List<AuditCall> calls) =>
        new(
            db,
            new BackupKnownGapsCatalog(),
            AuditCapturing(calls).Object,
            NullLogger<BackupKnownGapAcceptanceService>.Instance);

    private static Mock<IAuditLogService> AuditCapturing(List<AuditCall> calls)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Callback(new InvocationAction(invocation =>
            {
                calls.Add(new AuditCall(
                    invocation.Arguments[12] as AuditEventType?,
                    invocation.Arguments[14] as Guid?));
            }))
            .ReturnsAsync(new AuditLog());
        return audit;
    }

    private static AdminTenantDetailDto Detail(Guid id) => new(
        Id: id,
        Name: "dev",
        Slug: "dev",
        Email: null,
        Phone: null,
        Address: null,
        Status: "active",
        IsActive: true,
        LicenseKey: null,
        LicenseValidUntilUtc: null,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: null,
        DeletedAtUtc: null);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BackupKnownGaps_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(Guid.NewGuid()));
    }

    private static Tenant Tenant(Guid id, string slug) => new()
    {
        Id = id,
        Name = slug,
        Slug = slug,
        IsActive = true,
    };

    private sealed record AuditCall(AuditEventType? Type, Guid? TenantId);

    private sealed class FixedTime(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class FixedMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestHostEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = ".";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
