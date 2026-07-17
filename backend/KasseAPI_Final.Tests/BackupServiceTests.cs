using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task CreateBackupAsync_fails_when_tenant_missing()
    {
        var (sut, _) = CreateSut(nameof(CreateBackupAsync_fails_when_tenant_missing));
        var result = await sut.CreateBackupAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result.Succeeded);
        Assert.Equal(BackupService.TenantNotFoundCode, result.Code);
    }

    [Fact]
    public async Task CreateBackupAsync_enqueues_when_tenant_exists()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var (sut, db) = CreateSut(
            nameof(CreateBackupAsync_enqueues_when_tenant_exists),
            trigger: outcome =>
            {
                outcome.Setup(t => t.RequestManualBackupAsync(
                        It.IsAny<string?>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<BackupStrategyKind?>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new BackupManualTriggerOutcome
                    {
                        Run = new BackupRun
                        {
                            Id = runId,
                            Status = BackupRunStatus.Queued,
                            TriggerSource = BackupTriggerSource.Manual,
                            AdapterKind = "PgDump",
                            Strategy = BackupStrategyKind.Tenant,
                            RequestedAt = DateTime.UtcNow,
                            TenantId = tenantId
                        },
                        Kind = BackupManualTriggerResultKind.NewRunQueued
                    });
            });

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            Slug = "acme",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await sut.CreateBackupAsync(tenantId, userId);
        Assert.True(result.Succeeded);
        Assert.Equal(runId, result.BackupRunId);
        Assert.Equal(BackupManualTriggerResultKind.NewRunQueued, result.TriggerKind);
    }

    [Fact]
    public async Task CreateSystemBackupAsync_enqueues_deployment_wide()
    {
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var (sut, _) = CreateSut(
            nameof(CreateSystemBackupAsync_enqueues_deployment_wide),
            trigger: outcome =>
            {
                outcome.Setup(t => t.RequestManualBackupAsync(
                        It.IsAny<string?>(),
                        Roles.SuperAdmin,
                        It.Is<string?>(k => k != null && k.StartsWith("manual-system-", StringComparison.Ordinal)),
                        It.IsAny<string?>(),
                        BackupStrategyKind.System,
                        true,
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new BackupManualTriggerOutcome
                    {
                        Run = new BackupRun
                        {
                            Id = runId,
                            Status = BackupRunStatus.Queued,
                            TriggerSource = BackupTriggerSource.Manual,
                            AdapterKind = "PgDump",
                            Strategy = BackupStrategyKind.System,
                            RequestedAt = DateTime.UtcNow,
                            TenantId = null
                        },
                        Kind = BackupManualTriggerResultKind.NewRunQueued
                    });
            });

        var result = await sut.CreateSystemBackupAsync(userId);
        Assert.True(result.Succeeded);
        Assert.Equal(runId, result.BackupRunId);
    }

    [Fact]
    public async Task ListBackupsAsync_tenant_admin_sees_only_own_tenant_strategy()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (sut, db) = CreateSut(nameof(ListBackupsAsync_tenant_admin_sees_only_own_tenant_strategy));

        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantA,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                RequestedAt = DateTime.UtcNow.AddHours(-1)
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantB,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                RequestedAt = DateTime.UtcNow.AddHours(-2)
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Strategy = BackupStrategyKind.System,
                TenantId = null,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "PgDump",
                RequestedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var list = await sut.ListBackupsAsync(tenantA, isSuperAdmin: false, page: 1, pageSize: 20);
        Assert.Equal(1, list.TotalCount);
        Assert.All(list.Items, i =>
        {
            Assert.Equal(BackupStrategyKind.Tenant, i.Strategy);
            Assert.Equal(tenantA, i.TenantId);
        });
    }

    [Fact]
    public async Task ListBackupsAsync_super_admin_sees_all()
    {
        var tenantA = Guid.NewGuid();
        var (sut, db) = CreateSut(nameof(ListBackupsAsync_super_admin_sees_all));

        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantA,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                RequestedAt = DateTime.UtcNow.AddHours(-1)
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Strategy = BackupStrategyKind.System,
                TenantId = null,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "PgDump",
                RequestedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var list = await sut.ListBackupsAsync(null, isSuperAdmin: true, page: 1, pageSize: 20);
        Assert.Equal(2, list.TotalCount);
    }

    [Fact]
    public async Task RestoreBackupAsync_rejects_cross_tenant()
    {
        var backupTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var backupId = Guid.NewGuid();
        var (sut, db) = CreateSut(nameof(RestoreBackupAsync_rejects_cross_tenant));

        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            TenantId = backupTenant,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "x.dump",
                    ContentHashSha256 = new string('a', 64),
                    ByteSize = 10
                }
            }
        });
        await db.SaveChangesAsync();

        var result = await sut.RestoreBackupAsync(backupId, otherTenant, Guid.NewGuid());
        Assert.False(result.Succeeded);
        Assert.Equal(RestoreService.CrossTenantCode, result.Code);
    }

    [Fact]
    public async Task RestoreBackupAsync_queues_validation_request_when_compliant()
    {
        var tenantId = Guid.NewGuid();
        var backupId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var restore = new Mock<IManualRestoreTriggerService>();
        restore.Setup(r => r.CreateRequestAsync(
                It.IsAny<RestoreRequest>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreRequestStatus
            {
                RequestId = requestId,
                Status = "PendingApproval",
                BackupRunId = backupId,
                TargetDatabaseName = "restore_validation_test",
                ValidationOnly = true,
                RequestedAt = DateTime.UtcNow
            });

        var (sut, db) = CreateSut(
            nameof(RestoreBackupAsync_queues_validation_request_when_compliant),
            manualRestore: restore);

        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            TenantId = tenantId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "ok.dump",
                    ContentHashSha256 = new string('b', 64),
                    ByteSize = 100
                }
            }
        });
        await db.SaveChangesAsync();

        var result = await sut.RestoreBackupAsync(backupId, tenantId, Guid.NewGuid());
        Assert.True(result.Succeeded);
        Assert.Equal(requestId, result.RestoreRequestId);
        Assert.Equal(backupId, result.BackupRunId);
        Assert.Contains("second Super Admin", result.PreviewNote, StringComparison.OrdinalIgnoreCase);
    }

    private static (BackupService Sut, AppDbContext Db) CreateSut(
        string dbName,
        Action<Mock<IBackupManualTriggerService>>? trigger = null,
        Mock<IManualRestoreTriggerService>? manualRestore = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        var triggerMock = new Mock<IBackupManualTriggerService>();
        trigger?.Invoke(triggerMock);

        var restoreMock = manualRestore ?? new Mock<IManualRestoreTriggerService>();
        var checksum = new Mock<IBackupChecksumService>();
        checksum.Setup(c => c.FileMatchesSha256Async(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var compliance = new ComplianceCheckService(
            db,
            new RestoreService(),
            checksum.Object,
            new FixedOptionsMonitor<BackupOptions>(new BackupOptions { ArtifactStagingRoot = null }),
            NullLogger<ComplianceCheckService>.Instance);

        var sut = new BackupService(
            db,
            triggerMock.Object,
            compliance,
            restoreMock.Object,
            new BackupStagingDiskMonitor(),
            new FixedOptionsMonitor<BackupOptions>(new BackupOptions { ArtifactStagingRoot = null }),
            NullLogger<BackupService>.Instance);

        return (sut, db);
    }

    private sealed class FixedOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        public FixedOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
