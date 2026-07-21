using System.Text.Json.Nodes;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
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
        services.AddSingleton(OptionsMonitorOf(new BackupOptions { ExecutionAdapterKind = BackupExecutionAdapterKind.Fake }));
        services.AddSingleton<IHostEnvironment>(_ =>
        {
            var m = new Mock<IHostEnvironment>();
            m.Setup(h => h.EnvironmentName).Returns(Environments.Development);
            return m.Object;
        });
        services.AddLogging(b => { });
        services.AddSingleton<ISmartRetentionService, SmartRetentionService>();
        services.AddSingleton<IStorageTierService, StorageTierService>();
        services.AddScoped<IBackupPostSuccessOrchestrationHook, BackupPostSuccessOrchestrationHook>();
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
            Strategy = BackupStrategyKind.System,
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
            new BackupEncryptionService(
                OptionsMonitorOf(new BackupOptions()),
                NullLogger<BackupEncryptionService>.Instance),
            NullLogger<PostgreSqlPgDumpBackupExecutionAdapter>.Instance);

        var ext = new Mock<IBackupArtifactExternalArchive>();
        ext.SetupGet(x => x.BackendDescriptor).Returns(BackupExternalArchiveBackendDescriptors.Filesystem);
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
            new TenantScopedLogicalBackupExecutionAdapter(
                scopeFactory,
                OptionsMonitorOf(new BackupOptions()),
                new TenantScopedBackupExporter(),
                checksum,
                new BackupEncryptionService(
                    OptionsMonitorOf(new BackupOptions()),
                    NullLogger<BackupEncryptionService>.Instance),
                NullLogger<TenantScopedLogicalBackupExecutionAdapter>.Instance),
            new CompositeSystemBackupExecutionAdapter(
                pgDump,
                scopeFactory,
                OptionsMonitorOf(new BackupOptions()),
                new SystemScopedBackupExporter(new TenantScopedBackupExporter()),
                checksum,
                NullLogger<CompositeSystemBackupExecutionAdapter>.Instance),
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
        var fake = new FakeBackupExecutionAdapter(OptionsMonitorOf(new BackupOptions()), checksum, new BackupEncryptionService(OptionsMonitorOf(new BackupOptions()), NullLogger<BackupEncryptionService>.Instance))
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
        Assert.Contains("\"schemaVersion\":4", run.ConfigSnapshotJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backup_verifier_throw_marks_run_verification_failed_not_stuck()
    {
        var dbName = $"orch_bk_v_{Guid.NewGuid():N}";
        var checksum = new BackupChecksumService();
        var fake = new FakeBackupExecutionAdapter(OptionsMonitorOf(new BackupOptions()), checksum, new BackupEncryptionService(OptionsMonitorOf(new BackupOptions()), NullLogger<BackupEncryptionService>.Instance));
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
        var fake = new FakeBackupExecutionAdapter(OptionsMonitorOf(new BackupOptions()), checksum, new BackupEncryptionService(OptionsMonitorOf(new BackupOptions()), NullLogger<BackupEncryptionService>.Instance));
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
                Mock.Of<IPostRestoreDrillSqlChecker>(),
                Mock.Of<IRestoredDatabaseApplicationSmokeRunner>(),
                Mock.Of<IFiscalGoLiveValidationRunner>(),
                Mock.Of<IApplicationRecoverySmokeProbe>(),
                RestoreVerificationTestDoubles.ExternalDependencyEvidence(),
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
            Assert.Contains("\"schemaVersion\":4", run.ConfigSnapshotJson, StringComparison.Ordinal);
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

    /// <summary>
    /// Regresyon: Aynı <see cref="JsonNode"/> iki anahtara eklenemez (InvalidOperationException: parent zaten var).
    /// pg_restore --list başarısından sonra detaylar yazılmalı — çalıştırma Succeeded ile biter.
    /// </summary>
    [Fact]
    public async Task Restore_drill_pg_restore_list_success_succeeds_and_details_has_two_independent_inspection_nodes()
    {
        var dbName = $"orch_rv_ok_{Guid.NewGuid():N}";
        var temp = Directory.CreateTempSubdirectory($"rv_ok_{Guid.NewGuid():N}");
        try
        {
            var dumpFile = Path.Combine(temp.FullName, "logical.dump");
            await File.WriteAllTextAsync(dumpFile, "x");

            var backupRunId = Guid.NewGuid();
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
                db.RestoreVerificationRuns.Add(new RestoreVerificationRun
                {
                    Status = RestoreVerificationStatus.Queued,
                    TriggerSource = RestoreVerificationTriggerSource.Manual,
                    RequestedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var listMock = new Mock<IPgRestoreListInspector>();
            listMock
                .Setup(x => x.InspectDumpFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PgRestoreListInspectResult
                {
                    Success = true,
                    ExitCode = 0,
                    NonEmptyLineCount = 7,
                    StdErrSnippet = null
                });

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
            var orchestrator = new RestoreVerificationOrchestratorHostedService(
                scopeFactory,
                OptionsMonitorOf(restoreOpts),
                OptionsMonitorOf(backupOpts),
                hostEnv.Object,
                new ConfigurationBuilder().Build(),
                Mock.Of<IRestoreVerificationOrchestratorDistributedLock>(),
                listMock.Object,
                Mock.Of<IPgRestoreIsolatedRestoreRunner>(),
                Mock.Of<IPostRestoreDrillSqlChecker>(),
                Mock.Of<IRestoredDatabaseApplicationSmokeRunner>(),
                Mock.Of<IFiscalGoLiveValidationRunner>(),
                Mock.Of<IApplicationRecoverySmokeProbe>(),
                RestoreVerificationTestDoubles.ExternalDependencyEvidence(),
                restoreReadiness,
                Mock.Of<IRestoreVerificationOrchestratorMetrics>(),
                Mock.Of<IBackupAlertPublisher>(),
                NullLogger<RestoreVerificationOrchestratorHostedService>.Instance);

            await orchestrator.ProcessNextExclusiveBodyAsync(CancellationToken.None);

            await using var assertScope = scopeFactory.CreateAsyncScope();
            var dbOut = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await dbOut.RestoreVerificationRuns.AsNoTracking().SingleAsync();
            Assert.Equal(RestoreVerificationStatus.Succeeded, run.Status);
            Assert.Null(run.FailureCode);
            Assert.False(string.IsNullOrWhiteSpace(run.DetailsJson));
            Assert.False(string.IsNullOrWhiteSpace(run.EvidenceJson));
            Assert.Contains("\"schemaVersion\":5", run.EvidenceJson, StringComparison.Ordinal);
            Assert.NotNull(run.SourceBackupArtifactId);

            var root = JsonNode.Parse(run.DetailsJson!) as JsonObject;
            Assert.NotNull(root);
            var pg = root!["pgRestoreList"];
            var di = root["dumpInspection"];
            Assert.NotNull(pg);
            Assert.NotNull(di);
            // İki ayrı anahtar aynı içeriği taşımalı (SerializeToNode iki kez); tek JsonNode iki kez eklenemezdi.
            Assert.Equal(pg!.ToJsonString(), di!.ToJsonString());
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
