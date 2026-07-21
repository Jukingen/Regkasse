using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ManualRestoreTriggerServiceTests
{
    [Fact]
    public async Task CreateRequestAsync_logs_restore_requested_audit()
    {
        var dbName = $"mr_audit_{Guid.NewGuid():N}";
        var (svc, db, audit) = CreateSutWithAudit(dbName);
        var backupId = Guid.NewGuid();
        SeedSucceededSystemBackup(db, backupId);
        await db.SaveChangesAsync();

        await svc.CreateRequestAsync(
            new RestoreRequest
            {
                BackupRunId = backupId,
                TargetDatabaseName = "restore_validation_audit_test",
                ValidationOnly = true,
                Reason = "integrity check"
            },
            "requester",
            "requester@test.com",
            "corr-1");

        audit.Verify(
            a => a.LogSystemOperationAsync(
                AuditLogActions.RESTORE_REQUESTED,
                AuditLogEntityTypes.MANUAL_RESTORE_REQUEST,
                "requester",
                Roles.SuperAdmin,
                It.IsAny<string>(),
                It.IsAny<string>(),
                AuditLogStatus.Success,
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                "corr-1",
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.RestoreRequested,
                It.IsAny<Guid?>(),
                LegacyDefaultTenantIds.Primary),
            Times.Once);
    }

    [Fact]
    public async Task CreateRequestAsync_stamps_source_backup_tenant_on_audit_when_run_is_tenant_scoped()
    {
        var dbName = $"mr_tenant_audit_{Guid.NewGuid():N}";
        var (svc, db, audit) = CreateSutWithAudit(dbName);
        var backupId = Guid.NewGuid();
        var sourceTenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        SeedSucceededSystemBackup(db, backupId, sourceTenantId);
        await db.SaveChangesAsync();

        object? capturedRequestData = null;
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AuditLogStatus>(), It.IsAny<string>(),
                It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
            .Callback<string, string, string, string, string?, string?, AuditLogStatus, string?, object?, object?, string?, ImpersonationAuditContext.Snapshot?, AuditEventType?, Guid?, Guid?>(
                (_, _, _, _, _, _, _, _, requestData, _, _, _, _, _, _) => capturedRequestData = requestData)
            .ReturnsAsync(new AuditLog());

        await svc.CreateRequestAsync(
            new RestoreRequest
            {
                BackupRunId = backupId,
                TargetDatabaseName = "restore_validation_tenant_audit",
                ValidationOnly = true,
                Reason = "tenant-scoped restore audit"
            },
            "requester",
            "requester@test.com",
            "corr-tenant");

        audit.Verify(
            a => a.LogSystemOperationAsync(
                AuditLogActions.RESTORE_REQUESTED,
                AuditLogEntityTypes.MANUAL_RESTORE_REQUEST,
                "requester",
                Roles.SuperAdmin,
                It.IsAny<string>(),
                It.IsAny<string>(),
                AuditLogStatus.Success,
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                "corr-tenant",
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.RestoreRequested,
                It.IsAny<Guid?>(),
                sourceTenantId),
            Times.Once);

        Assert.NotNull(capturedRequestData);
        var json = System.Text.Json.JsonSerializer.Serialize(capturedRequestData);
        Assert.Contains(sourceTenantId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tenant_access_scoped", json, StringComparison.Ordinal);
        Assert.Contains("no_fiscal_timestamp_rewrite", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRestoreAuditTenantId_uses_platform_tenant_for_deployment_wide_dump()
    {
        Assert.Equal(
            LegacyDefaultTenantIds.Primary,
            ManualRestoreAudit.ResolveRestoreAuditTenantId(null));
        Assert.Equal(
            "deployment_wide",
            ManualRestoreAudit.ResolveRestoreScope(null));
    }

    [Fact]
    public async Task CreateRequestAsync_rejects_validation_only_false()
    {
        var (svc, _) = CreateSut(Guid.NewGuid().ToString());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateRequestAsync(
                new RestoreRequest
                {
                    BackupRunId = Guid.NewGuid(),
                    TargetDatabaseName = "restore_validation_test",
                    ValidationOnly = false
                },
                "u1",
                "a@test.com",
                null));
        Assert.Contains("ValidationOnly", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestAsync_rejects_cross_tenant_with_not_found()
    {
        var dbName = $"mr_xtenant_{Guid.NewGuid():N}";
        var backupTenant = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var otherTenant = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var (svc, db) = CreateSut(dbName, ambientTenantId: otherTenant);
        var backupId = Guid.NewGuid();
        SeedSucceededSystemBackup(db, backupId, backupTenant);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateRequestAsync(
                new RestoreRequest
                {
                    BackupRunId = backupId,
                    TargetDatabaseName = "restore_validation_xtenant",
                    ValidationOnly = true
                },
                "requester",
                "requester@test.com",
                null));

        Assert.Contains(backupId.ToString(), ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, await db.ManualRestoreRequests.CountAsync());
    }

    [Fact]
    public async Task ApproveAsync_requires_different_super_admin()
    {
        var dbName = $"mr_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName);
        var backupId = Guid.NewGuid();
        SeedSucceededSystemBackup(db, backupId);
        await db.SaveChangesAsync();

        var created = await svc.CreateRequestAsync(
            new RestoreRequest
            {
                BackupRunId = backupId,
                TargetDatabaseName = "restore_validation_approve_test",
                ValidationOnly = true
            },
            "requester",
            "requester@test.com",
            null);

        var entity = await db.ManualRestoreRequests.FirstAsync(r => r.Id == created.RequestId);
        const string plainToken = "123456";
        entity.ApprovalTokenHash = ManualRestoreApprovalTokenHasher.Hash(plainToken);
        entity.ApprovalTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProcessApprovalAsync(
                created.RequestId,
                new RestoreApprovalRequest
                {
                    ApprovalToken = plainToken,
                    Action = "approve"
                },
                "requester",
                "requester@test.com",
                null));
    }

    private static (ManualRestoreTriggerService Svc, AppDbContext Db, Mock<IAuditLogService> Audit) CreateSutWithAudit(string dbName)
    {
        var (svc, db, audit) = CreateSutCore(dbName, ambientTenantId: null);
        return (svc, db, audit);
    }

    private static (ManualRestoreTriggerService Svc, AppDbContext Db) CreateSut(
        string dbName,
        Guid? ambientTenantId = null)
    {
        var (svc, db, _) = CreateSutCore(dbName, ambientTenantId);
        return (svc, db);
    }

    private static (ManualRestoreTriggerService Svc, AppDbContext Db, Mock<IAuditLogService> Audit) CreateSutCore(
        string dbName,
        Guid? ambientTenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=regkasse_app;Username=u;Password=p"
            })
            .Build();

        var manualOpts = Options.Create(new ManualRestoreApprovalOptions { Enabled = true });
        var restoreOpts = Options.Create(new RestoreVerificationOptions());
        var guard = new ManualRestoreTargetDatabaseGuard(config, new StubMonitor<ManualRestoreApprovalOptions>(manualOpts.Value));
        var notification = new Mock<IManualRestoreApprovalNotificationService>();
        notification.Setup(n => n.SendApprovalTokenAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<ManualRestoreApprovalNotificationContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AuditLogStatus>(), It.IsAny<string>(),
                It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new AuditLog());

        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.SetupProperty(t => t.TenantId, ambientTenantId);

        var checksum = new Mock<IBackupChecksumService>();
        checksum.Setup(c => c.FileMatchesSha256Async(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var compliance = new ComplianceCheckService(
            db,
            new RestoreService(),
            checksum.Object,
            new StubMonitor<BackupOptions>(new BackupOptions { ArtifactStagingRoot = null }),
            NullLogger<ComplianceCheckService>.Instance);

        var svc = new ManualRestoreTriggerService(
            db,
            audit.Object,
            compliance,
            tenantAccessor.Object,
            guard,
            notification.Object,
            new StubMonitor<ManualRestoreApprovalOptions>(manualOpts.Value),
            new StubMonitor<RestoreVerificationOptions>(restoreOpts.Value),
            NullLogger<ManualRestoreTriggerService>.Instance);

        return (svc, db, audit);
    }

    private static void SeedSucceededSystemBackup(AppDbContext db, Guid backupId, Guid? tenantId = null)
    {
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            TenantId = tenantId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            Strategy = BackupStrategyKind.System,
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = $"{backupId:N}.dump",
                    ContentHashSha256 = new string('a', 64),
                    ByteSize = 10
                }
            }
        });
    }

    private sealed class StubMonitor<T> : IOptionsMonitor<T> where T : class, new()
    {
        public StubMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
