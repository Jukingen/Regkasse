using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupPhase1OrchestrationTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"backup_phase1_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static IOptionsMonitor<BackupOptions> OptionsMonitor(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    [Fact]
    public async Task Manual_trigger_creates_queued_run()
    {
        await using var db = CreateDb();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var svc = new BackupManualTriggerService(
            db,
            audit.Object,
            Mock.Of<ICurrentTenantAccessor>(),
            OptionsMonitor(new BackupOptions()),
            Mock.Of<IBackupAlertPublisher>(),
            NullLogger<BackupManualTriggerService>.Instance);

        var outcome = await svc.RequestManualBackupAsync("u1", "SuperAdmin", null, "corr-1", cancellationToken: default);

        Assert.Equal(BackupManualTriggerResultKind.NewRunQueued, outcome.Kind);
        Assert.Equal(BackupRunStatus.Queued, outcome.Run.Status);
        Assert.Equal(BackupTriggerSource.Manual, outcome.Run.TriggerSource);
        Assert.NotNull(outcome.Run.ConfigSnapshotJson);
        Assert.Contains("backup_manual_enqueue", outcome.Run.ConfigSnapshotJson, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\":4", outcome.Run.ConfigSnapshotJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Manual_trigger_without_client_key_encodes_ambient_tenant()
    {
        var tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await using var db = CreateDb();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var tenantAccessor = new NullCurrentTenantAccessor { TenantId = tenantId };
        var svc = new BackupManualTriggerService(
            db,
            audit.Object,
            tenantAccessor,
            OptionsMonitor(new BackupOptions()),
            Mock.Of<IBackupAlertPublisher>(),
            NullLogger<BackupManualTriggerService>.Instance);

        var outcome = await svc.RequestManualBackupAsync("u1", "Manager", null, "corr-tenant", cancellationToken: default);

        Assert.Equal(BackupManualTriggerResultKind.NewRunQueued, outcome.Kind);
        Assert.NotNull(outcome.Run.IdempotencyKey);
        Assert.Contains($"manual-tenant-{tenantId:D}", outcome.Run.IdempotencyKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(tenantId, outcome.Run.TenantId);
    }

    [Fact]
    public async Task Different_tenants_can_enqueue_manual_backups_while_other_tenant_active()
    {
        var tenantA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        await using var db = CreateDb();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var accessorA = new NullCurrentTenantAccessor { TenantId = tenantA };
        var svcA = new BackupManualTriggerService(
            db,
            audit.Object,
            accessorA,
            OptionsMonitor(new BackupOptions()),
            Mock.Of<IBackupAlertPublisher>(),
            NullLogger<BackupManualTriggerService>.Instance);

        var first = await svcA.RequestManualBackupAsync("manager-a", "Manager", null, "corr-a", cancellationToken: default);
        Assert.Equal(BackupManualTriggerResultKind.NewRunQueued, first.Kind);

        var accessorB = new NullCurrentTenantAccessor { TenantId = tenantB };
        var svcB = new BackupManualTriggerService(
            db,
            audit.Object,
            accessorB,
            OptionsMonitor(new BackupOptions()),
            Mock.Of<IBackupAlertPublisher>(),
            NullLogger<BackupManualTriggerService>.Instance);

        var second = await svcB.RequestManualBackupAsync("manager-b", "Manager", null, "corr-b", cancellationToken: default);
        Assert.Equal(BackupManualTriggerResultKind.NewRunQueued, second.Kind);
        Assert.NotEqual(first.Run.Id, second.Run.Id);
        Assert.Equal(2, await db.BackupRuns.CountAsync());
    }

    [Fact]
    public async Task Second_manual_trigger_while_queued_returns_duplicate_prevented()
    {
        await using var db = CreateDb();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var alerts = new Mock<IBackupAlertPublisher>();
        var svc = new BackupManualTriggerService(
            db,
            audit.Object,
            Mock.Of<ICurrentTenantAccessor>(),
            OptionsMonitor(new BackupOptions()),
            alerts.Object,
            NullLogger<BackupManualTriggerService>.Instance);

        var first = await svc.RequestManualBackupAsync("u1", "SuperAdmin", null, "c1", cancellationToken: default);
        var second = await svc.RequestManualBackupAsync("u1", "SuperAdmin", null, "c2", cancellationToken: default);

        Assert.Equal(BackupManualTriggerResultKind.NewRunQueued, first.Kind);
        Assert.Equal(BackupManualTriggerResultKind.DuplicateActiveManualPrevented, second.Kind);
        Assert.Equal(first.Run.Id, second.Run.Id);
        alerts.Verify(a => a.Publish(It.IsAny<BackupAlertEvent>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Idempotency_key_returns_same_run_without_duplicate_flag()
    {
        await using var db = CreateDb();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var svc = new BackupManualTriggerService(
            db,
            audit.Object,
            Mock.Of<ICurrentTenantAccessor>(),
            OptionsMonitor(new BackupOptions()),
            Mock.Of<IBackupAlertPublisher>(),
            NullLogger<BackupManualTriggerService>.Instance);

        var key = "idem-1";
        var a = await svc.RequestManualBackupAsync("u1", "SuperAdmin", key, "c1", cancellationToken: default);
        var b = await svc.RequestManualBackupAsync("u1", "SuperAdmin", key, "c2", cancellationToken: default);

        Assert.Equal(a.Run.Id, b.Run.Id);
        Assert.Equal(BackupManualTriggerResultKind.IdempotentReplay, b.Kind);
    }

    [Fact]
    public async Task Fake_adapter_and_verifier_success_path()
    {
        var checksum = new BackupChecksumService();
        var adapter = new FakeBackupExecutionAdapter(OptionsMonitor(new BackupOptions()), checksum, new BackupEncryptionService(OptionsMonitor(new BackupOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupEncryptionService>.Instance));
        var ctx = new BackupExecutionContext(Guid.NewGuid(), "c", adapter.AdapterKind, default);
        var exec = await adapter.ExecuteAsync(ctx);
        Assert.True(exec.Success);
        Assert.NotEmpty(exec.Artifacts);

        var verifier = new BackupVerificationService(OptionsMonitor(new BackupOptions()), checksum);
        var v = await verifier.VerifyArtifactsAsync(ctx.BackupRunId, exec.Artifacts, default);
        Assert.True(v.Passed);
    }

    [Fact]
    public async Task Verifier_passed_with_manifest_only_has_completeness_false()
    {
        const string hash64 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var artifacts = new[]
        {
            new BackupArtifactDescriptor
            {
                ArtifactType = BackupArtifactType.VerificationManifest,
                StorageDescriptor = "manifest-only.json",
                ByteSize = 1,
                ContentHashSha256 = hash64,
                MetadataJson = "{}",
                RequireOnDiskHashVerification = false
            }
        };

        var verifier = new BackupVerificationService(OptionsMonitor(new BackupOptions()), new BackupChecksumService());
        var v = await verifier.VerifyArtifactsAsync(Guid.NewGuid(), artifacts, default);
        Assert.True(v.Passed);
        Assert.False(v.CompletenessFlag);
    }

    [Fact]
    public async Task Verifier_fails_when_forced_by_options()
    {
        var checksum = new BackupChecksumService();
        var adapter = new FakeBackupExecutionAdapter(OptionsMonitor(new BackupOptions()), checksum, new BackupEncryptionService(OptionsMonitor(new BackupOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupEncryptionService>.Instance));
        var ctx = new BackupExecutionContext(Guid.NewGuid(), "c", adapter.AdapterKind, default);
        var exec = await adapter.ExecuteAsync(ctx);

        var verifier = new BackupVerificationService(OptionsMonitor(new BackupOptions
        {
            DevelopmentForceVerificationFailure = true
        }), checksum);
        var v = await verifier.VerifyArtifactsAsync(ctx.BackupRunId, exec.Artifacts, default);
        Assert.False(v.Passed);
    }

    [Fact]
    public async Task Production_stub_adapter_returns_failure()
    {
        var stub = new PostgreSqlBackupExecutionAdapterStub();
        var exec = await stub.ExecuteAsync(new BackupExecutionContext(Guid.NewGuid(), null, stub.AdapterKind, default));
        Assert.False(exec.Success);
        Assert.Equal("ADAPTER_NOT_IMPLEMENTED", exec.ErrorCode);
    }

    [Fact]
    public async Task Query_latest_run_returns_most_recent()
    {
        await using var db = CreateDb();
        db.BackupRuns.Add(new BackupRun
        {
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow.AddHours(-2)
        });
        db.BackupRuns.Add(new BackupRun
        {
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var q = new BackupRunQueryService(db);
        var latest = await q.GetLatestRunAsync();
        Assert.NotNull(latest);
        Assert.Equal(BackupRunStatus.Queued, latest.Status);
    }
}
