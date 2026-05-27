using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.RestoreVerification;
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
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow
        });
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
                It.IsAny<Guid?>()),
            Times.Once);
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
    public async Task ApproveAsync_requires_different_super_admin()
    {
        var dbName = $"mr_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName);
        var backupId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow
        });
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
        var (svc, db, audit) = CreateSutCore(dbName);
        return (svc, db, audit);
    }

    private static (ManualRestoreTriggerService Svc, AppDbContext Db) CreateSut(string dbName)
    {
        var (svc, db, _) = CreateSutCore(dbName);
        return (svc, db);
    }

    private static (ManualRestoreTriggerService Svc, AppDbContext Db, Mock<IAuditLogService> Audit) CreateSutCore(string dbName)
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

        var svc = new ManualRestoreTriggerService(
            db,
            audit.Object,
            guard,
            notification.Object,
            new StubMonitor<ManualRestoreApprovalOptions>(manualOpts.Value),
            new StubMonitor<RestoreVerificationOptions>(restoreOpts.Value),
            NullLogger<ManualRestoreTriggerService>.Instance);

        return (svc, db, audit);
    }

    private sealed class StubMonitor<T> : IOptionsMonitor<T> where T : class, new()
    {
        public StubMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
