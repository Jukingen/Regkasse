using System.Collections.Generic;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.Backup.PgDump;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OrchestratorRunFinalizerAndHostedServiceTests
{
    private static IOptionsMonitor<T> OptionsMonitorOf<T>(T value) where T : class, new()
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static IServiceScopeFactory CreateScopeFactory(string inMemoryDbName, Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(inMemoryDbName));
        services.AddScoped<IBackupRunQueryService, BackupRunQueryService>();
        extra?.Invoke(services);
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IServiceScopeFactory>();
    }

    private static async Task SeedQueuedBackupRun(IServiceScopeFactory scopeFactory)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.BackupRuns.Add(new BackupRun
        {
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static BackupOrchestratorHostedService CreateBackupOrchestrator(
        IServiceScopeFactory scopeFactory,
        FakeBackupExecutionAdapter fakeAdapter,
        IBackupVerificationService verifier)
    {
        var checksum = new BackupChecksumService();
        var pgDump = new PostgreSqlPgDumpBackupExecutionAdapter(
            new ConfigurationBuilder().Build(),
            OptionsMonitorOf(new BackupOptions()),
            Mock.Of<IPgDumpProcessRunner>(),
            Mock.Of<IBackupManifestService>(),
            checksum,
            NullLogger<PostgreSqlPgDumpBackupExecutionAdapter>.Instance);

        var ext = new Mock<IBackupArtifactExternalArchive>();
        ext.Setup(x => x.CopyStagingArtifactsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<BackupArtifactDescriptor>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupExternalArchiveOutcome
            {
                Success = true,
                RedactedLocators = new Dictionary<BackupArtifactType, string>()
            });

        var hostEnv = new Mock<IHostEnvironment>();
        hostEnv.Setup(h => h.EnvironmentName).Returns(Environments.Development);

        return new BackupOrchestratorHostedService(
            scopeFactory,
            Mock.Of<IBackupOrchestratorDistributedLock>(),
            OptionsMonitorOf(new BackupOptions { ExecutionAdapterKind = BackupExecutionAdapterKind.Fake }),
            fakeAdapter,
            new PostgreSqlBackupExecutionAdapterStub(),
            pgDump,
            ext.Object,
            hostEnv.Object,
            Mock.Of<IBackupAlertPublisher>(),
            Mock.Of<IBackupOrchestratorMetrics>(),
            NullLogger<BackupOrchestratorHostedService>.Instance);
    }

    [Fact]
    public async Task Backup_adapter_throw_marks_run_failed_unhandled_exception()
    {
        var dbName = $"orch_bk_{Guid.NewGuid():N}";
        var checksum = new BackupChecksumService();
        var fake = new FakeBackupExecutionAdapter(OptionsMonitorOf(new BackupOptions()), checksum)
        {
            ThrowOnExecuteForTests = new InvalidOperationException("adapter boom")
        };
        var verifier = new BackupVerificationService(OptionsMonitorOf(new BackupOptions()), checksum);

        var scopeFactory = CreateScopeFactory(dbName, s => s.AddSingleton<IBackupVerificationService>(_ => verifier));
        await SeedQueuedBackupRun(scopeFactory);

        var orchestrator = CreateBackupOrchestrator(scopeFactory, fake, verifier);
        await orchestrator.ProcessNextExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = scopeFactory.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.BackupRuns.AsNoTracking().SingleAsync();
        Assert.Equal(BackupRunStatus.Failed, run.Status);
        Assert.Equal("UNHANDLED_EXCEPTION", run.FailureCode);
        Assert.Contains("adapter boom", run.FailureDetail ?? "", StringComparison.Ordinal);
        Assert.NotNull(run.ConfigSnapshotJson);
        Assert.Contains("backup_run_start", run.ConfigSnapshotJson, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\":1", run.ConfigSnapshotJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backup_verifier_throw_marks_run_verification_failed_not_stuck()
    {
        var dbName = $"orch_bk_v_{Guid.NewGuid():N}";
        var checksum = new BackupChecksumService();
        var fake = new FakeBackupExecutionAdapter(OptionsMonitorOf(new BackupOptions()), checksum);
        var verifier = new ThrowingBackupVerificationService();

        var scopeFactory = CreateScopeFactory(dbName, s => s.AddSingleton<IBackupVerificationService>(_ => verifier));
        await SeedQueuedBackupRun(scopeFactory);

        var orchestrator = CreateBackupOrchestrator(scopeFactory, fake, verifier);
        await orchestrator.ProcessNextExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = scopeFactory.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.BackupRuns.AsNoTracking().Include(r => r.Verifications).SingleAsync();
        Assert.Equal(BackupRunStatus.VerificationFailed, run.Status);
        Assert.Equal("VERIFIER_EXCEPTION", run.FailureCode);
        var v = Assert.Single(run.Verifications);
        Assert.Equal(BackupVerificationStatus.Failed, v.Status);
    }

    [Fact]
    public async Task Backup_unhandled_after_awaiting_verification_finalizes_to_verification_failed()
    {
        var dbName = $"orch_bk_j_{Guid.NewGuid():N}";
        var checksum = new BackupChecksumService();
        var fake = new FakeBackupExecutionAdapter(OptionsMonitorOf(new BackupOptions()), checksum);
        var verifier = new MalformedDetailsJsonVerificationService();

        var scopeFactory = CreateScopeFactory(dbName, s => s.AddSingleton<IBackupVerificationService>(_ => verifier));
        await SeedQueuedBackupRun(scopeFactory);

        var orchestrator = CreateBackupOrchestrator(scopeFactory, fake, verifier);
        await orchestrator.ProcessNextExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = scopeFactory.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.BackupRuns.AsNoTracking().Include(r => r.Verifications).SingleAsync();
        Assert.Equal(BackupRunStatus.VerificationFailed, run.Status);
        Assert.Equal("UNHANDLED_EXCEPTION", run.FailureCode);
    }

    [Fact]
    public async Task Backup_finalizer_is_idempotent_when_run_already_terminal()
    {
        var dbName = $"orch_bk_idem_{Guid.NewGuid():N}";
        await using (var scope = CreateScopeFactory(dbName).CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var id = Guid.NewGuid();
            db.BackupRuns.Add(new BackupRun
            {
                Id = id,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using var a = CreateScopeFactory(dbName).CreateAsyncScope();
        var dbA = a.ServiceProvider.GetRequiredService<AppDbContext>();
        await BackupOrchestratorRunFinalizer.TryFinalizeUnhandledExceptionAsync(
            dbA,
            (await dbA.BackupRuns.SingleAsync()).Id,
            new Exception("should not apply"),
            NullLogger.Instance,
            CancellationToken.None);

        await using var b = CreateScopeFactory(dbName).CreateAsyncScope();
        var dbB = b.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await dbB.BackupRuns.AsNoTracking().SingleAsync();
        Assert.Equal(BackupRunStatus.Succeeded, run.Status);
        Assert.Null(run.FailureCode);
    }

    [Fact]
    public async Task Restore_drill_unhandled_exception_finalizes_run_failed()
    {
        var dbName = $"orch_rv_{Guid.NewGuid():N}";
        var temp = Directory.CreateTempSubdirectory($"rv_{Guid.NewGuid():N}");
        try
        {
            var dumpFile = Path.Combine(temp.FullName, "logical.dump");
            await File.WriteAllTextAsync(dumpFile, "x");

            var backupRunId = Guid.NewGuid();
            Guid restoreRunId;
            await using (var scope = CreateScopeFactory(dbName).CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.BackupRuns.Add(new BackupRun
                {
                    Id = backupRunId,
                    Status = BackupRunStatus.Succeeded,
                    TriggerSource = BackupTriggerSource.Manual,
                    AdapterKind = "Fake",
                    RequestedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });
                db.BackupArtifacts.Add(new BackupArtifact
                {
                    BackupRunId = backupRunId,
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "logical.dump",
                    LifecycleState = BackupArtifactLifecycleState.ExternalCopyVerified,
                    CreatedAt = DateTime.UtcNow
                });
                var rv = new RestoreVerificationRun
                {
                    Status = RestoreVerificationStatus.Queued,
                    TriggerSource = RestoreVerificationTriggerSource.Manual,
                    RequestedAt = DateTime.UtcNow
                };
                db.RestoreVerificationRuns.Add(rv);
                await db.SaveChangesAsync();
                restoreRunId = rv.Id;
            }

            var listMock = new Mock<IPgRestoreListInspector>();
            listMock
                .Setup(x => x.InspectDumpFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("list boom"));

            var hostEnv = new Mock<IHostEnvironment>();
            hostEnv.Setup(h => h.EnvironmentName).Returns(Environments.Development);

            var backupOpts = new BackupOptions { ArtifactStagingRoot = temp.FullName };
            var scopeFactory = CreateScopeFactory(dbName);
            var restoreOpts = new RestoreVerificationOptions
            {
                IncludeLiveIntegrityChecks = false,
                IsolatedPgRestoreEnabled = false
            };
            var restoreReadiness = new RestoreVerificationOperationalReadinessService(
                OptionsMonitorOf(restoreOpts),
                hostEnv.Object);
            var alerts = new Mock<IBackupAlertPublisher>();
            var orchestrator = new RestoreVerificationOrchestratorHostedService(
                scopeFactory,
                OptionsMonitorOf(restoreOpts),
                OptionsMonitorOf(backupOpts),
                hostEnv.Object,
                new ConfigurationBuilder().Build(),
                Mock.Of<IRestoreVerificationOrchestratorDistributedLock>(),
                listMock.Object,
                Mock.Of<IPgRestoreIsolatedRestoreRunner>(),
                Mock.Of<IFiscalGoLiveValidationRunner>(),
                restoreReadiness,
                Mock.Of<IRestoreVerificationOrchestratorMetrics>(),
                alerts.Object,
                NullLogger<RestoreVerificationOrchestratorHostedService>.Instance);

            await orchestrator.ProcessNextExclusiveBodyAsync(CancellationToken.None);

            await using var assertScope = scopeFactory.CreateAsyncScope();
            var dbOut = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await dbOut.RestoreVerificationRuns.AsNoTracking().SingleAsync();
            Assert.Equal(RestoreVerificationStatus.Failed, run.Status);
            Assert.Equal("UNHANDLED_EXCEPTION", run.FailureCode);
            Assert.Contains("list boom", run.FailureDetail ?? "", StringComparison.Ordinal);
            Assert.NotNull(run.ConfigSnapshotJson);
            Assert.Contains("restore_run_start", run.ConfigSnapshotJson, StringComparison.Ordinal);
            Assert.Contains("\"schemaVersion\":1", run.ConfigSnapshotJson, StringComparison.Ordinal);
            alerts.Verify(
                a => a.Publish(It.Is<BackupAlertEvent>(e =>
                    e.Kind == BackupAlertKind.RestoreVerificationFailed
                    && e.RestoreVerificationRunId == restoreRunId
                    && e.Data != null
                    && e.Data["failureStage"] == "unhandled_orchestrator_exception")),
                Times.Once);
        }
        finally
        {
            try
            {
                temp.Delete(recursive: true);
            }
            catch
            {
                // ignore test cleanup failures
            }
        }
    }

    [Fact]
    public async Task Restore_finalizer_is_idempotent_when_run_already_terminal()
    {
        var dbName = $"orch_rv_idem_{Guid.NewGuid():N}";
        var runId = Guid.NewGuid();
        await using (var scope = CreateScopeFactory(dbName).CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RestoreVerificationRuns.Add(new RestoreVerificationRun
            {
                Id = runId,
                Status = RestoreVerificationStatus.Failed,
                TriggerSource = RestoreVerificationTriggerSource.Manual,
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                FailureCode = "PRIOR"
            });
            await db.SaveChangesAsync();
        }

        await using var a = CreateScopeFactory(dbName).CreateAsyncScope();
        var dbA = a.ServiceProvider.GetRequiredService<AppDbContext>();
        await RestoreVerificationOrchestratorRunFinalizer.TryFinalizeUnhandledExceptionAsync(
            dbA,
            runId,
            new Exception("ignored"),
            NullLogger.Instance,
            CancellationToken.None);

        await using var b = CreateScopeFactory(dbName).CreateAsyncScope();
        var dbB = b.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await dbB.RestoreVerificationRuns.AsNoTracking().SingleAsync();
        Assert.Equal(RestoreVerificationStatus.Failed, run.Status);
        Assert.Equal("PRIOR", run.FailureCode);
    }

    private sealed class ThrowingBackupVerificationService : IBackupVerificationService
    {
        public Task<BackupVerificationOutcome> VerifyArtifactsAsync(
            Guid backupRunId,
            IReadOnlyList<BackupArtifactDescriptor> artifacts,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("verify boom");
    }

    /// <summary>Geçerli doğrulama sonucu döner; orchestrator sonrası DetailsJson parse patlar — dış finalizer devreye girer.</summary>
    private sealed class MalformedDetailsJsonVerificationService : IBackupVerificationService
    {
        public Task<BackupVerificationOutcome> VerifyArtifactsAsync(
            Guid backupRunId,
            IReadOnlyList<BackupArtifactDescriptor> artifacts,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackupVerificationOutcome(true, true, null, "{not-json"));
    }
}
