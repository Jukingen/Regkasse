using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
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

/// <summary>
/// Restore drill: kısmi aşama başarısı asla terminal Succeeded ile karıştırılmamalı; kanıt ve hata kodları makine okunur.
/// </summary>
public sealed class RestoreVerificationDrillPipelineSemanticsTests
{
    private static IOptionsMonitor<T> OptionsMonitorOf<T>(T value) where T : class, new()
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static IServiceScopeFactory CreateScopeFactory(string inMemoryDbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(inMemoryDbName));
        services.AddScoped<IBackupRunQueryService, BackupRunQueryService>();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IServiceScopeFactory>();
    }

    private static IConfiguration ConfigWithConnection(string name, string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{name}"] = connectionString
            })
            .Build();

    [Fact]
    public async Task Pg_restore_list_failure_marks_failed_not_succeeded_with_PG_RESTORE_LIST_FAILED()
    {
        var dbName = $"rv_list_fail_{Guid.NewGuid():N}";
        var temp = Directory.CreateTempSubdirectory($"rv_lf_{Guid.NewGuid():N}");
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
                    Success = false,
                    ExitCode = 1,
                    NonEmptyLineCount = 0,
                    StdErrSnippet = "not a custom-format archive"
                });

            var hostEnv = new Mock<IHostEnvironment>();
            hostEnv.Setup(h => h.EnvironmentName).Returns(Environments.Development);

            var backupOpts = new BackupOptions { ArtifactStagingRoot = temp.FullName };
            var scopeFactory = CreateScopeFactory(dbName);
            var restoreOpts = new RestoreVerificationOptions
            {
                IncludeLiveIntegrityChecks = false,
                IsolatedPgRestoreEnabled = false,
                AllowNonPgDumpBackupSource = true
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
            Assert.Equal(RestoreVerificationStatus.Failed, run.Status);
            Assert.Equal("PG_RESTORE_LIST_FAILED", run.FailureCode);
            Assert.Equal(RestoreDrillFailureCategory.PgRestoreList, run.FailureCategory);
            Assert.NotNull(run.DetailsJson);
            Assert.Contains("pgRestoreList", run.DetailsJson, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                temp.Delete(recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task No_eligible_pg_dump_runs_fails_NO_ELIGIBLE_BACKUP_RUN_without_claiming_dump_proof()
    {
        var dbName = $"rv_no_elig_{Guid.NewGuid():N}";
        await using (var scope = CreateScopeFactory(dbName).CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RestoreVerificationRuns.Add(new RestoreVerificationRun
            {
                Status = RestoreVerificationStatus.Queued,
                TriggerSource = RestoreVerificationTriggerSource.Manual,
                RequestedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var hostEnv = new Mock<IHostEnvironment>();
        hostEnv.Setup(h => h.EnvironmentName).Returns(Environments.Development);
        var scopeFactory = CreateScopeFactory(dbName);
        var restoreOpts = new RestoreVerificationOptions
        {
            IncludeLiveIntegrityChecks = false,
            IsolatedPgRestoreEnabled = false,
            AllowNonPgDumpBackupSource = false,
            DumpFallbackDepth = 5
        };
        var restoreReadiness = new RestoreVerificationOperationalReadinessService(
            OptionsMonitorOf(restoreOpts),
            hostEnv.Object);
        var orchestrator = new RestoreVerificationOrchestratorHostedService(
            scopeFactory,
            OptionsMonitorOf(restoreOpts),
            OptionsMonitorOf(new BackupOptions()),
            hostEnv.Object,
            new ConfigurationBuilder().Build(),
            Mock.Of<IRestoreVerificationOrchestratorDistributedLock>(),
            Mock.Of<IPgRestoreListInspector>(),
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
        Assert.Equal(RestoreVerificationStatus.Failed, run.Status);
        Assert.Equal("NO_ELIGIBLE_BACKUP_RUN", run.FailureCode);
        Assert.Null(run.SourceBackupArtifactId);
    }

    [Fact]
    public async Task Isolated_pg_restore_process_failure_does_not_mark_succeeded()
    {
        var dbName = $"rv_iso_fail_{Guid.NewGuid():N}";
        var temp = Directory.CreateTempSubdirectory($"rv_if_{Guid.NewGuid():N}");
        const string adminCsName = "RvIsolatedAdmin";
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
                    NonEmptyLineCount = 3,
                    StdErrSnippet = null
                });

            var isolatedMock = new Mock<IPgRestoreIsolatedRestoreRunner>();
            isolatedMock
                .Setup(x => x.RestoreCustomDumpToEphemeralDatabaseAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PgRestoreIsolatedRestoreOutcome
                {
                    Success = false,
                    ExitCode = 2,
                    StdErrSnippet = "pg_restore: error: could not execute query"
                });

            var hostEnv = new Mock<IHostEnvironment>();
            hostEnv.Setup(h => h.EnvironmentName).Returns(Environments.Development);

            var backupOpts = new BackupOptions { ArtifactStagingRoot = temp.FullName };
            var scopeFactory = CreateScopeFactory(dbName);
            var restoreOpts = new RestoreVerificationOptions
            {
                IncludeLiveIntegrityChecks = false,
                IsolatedPgRestoreEnabled = true,
                IsolatedRestoreAdminConnectionStringName = adminCsName,
                PostRestoreSqlChecksEnabled = false,
                AllowNonPgDumpBackupSource = true
            };
            var restoreReadiness = new RestoreVerificationOperationalReadinessService(
                OptionsMonitorOf(restoreOpts),
                hostEnv.Object);
            var cfg = ConfigWithConnection(adminCsName, "Host=127.0.0.1;Port=5432;Username=u;Password=p;Database=postgres");
            var orchestrator = new RestoreVerificationOrchestratorHostedService(
                scopeFactory,
                OptionsMonitorOf(restoreOpts),
                OptionsMonitorOf(backupOpts),
                hostEnv.Object,
                cfg,
                Mock.Of<IRestoreVerificationOrchestratorDistributedLock>(),
                listMock.Object,
                isolatedMock.Object,
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
            Assert.Equal(RestoreVerificationStatus.Failed, run.Status);
            Assert.Equal("ISOLATED_PG_RESTORE_FAILED", run.FailureCode);
            Assert.True(run.RestoreAttemptExecuted);
            Assert.False(run.RestoreAttemptPassed);
        }
        finally
        {
            try
            {
                temp.Delete(recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task Restore_finalizer_idempotent_when_run_already_failed_with_evidence_json_absent()
    {
        var dbName = $"rv_idem_ev_{Guid.NewGuid():N}";
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
                FailureCode = "PRIOR",
                EvidenceJson = null
            });
            await db.SaveChangesAsync();
        }

        await using var a = CreateScopeFactory(dbName).CreateAsyncScope();
        var dbA = a.ServiceProvider.GetRequiredService<AppDbContext>();
        await RestoreVerificationOrchestratorRunFinalizer.TryFinalizeUnhandledExceptionAsync(
            dbA,
            runId,
            new Exception("must not apply"),
            NullLogger.Instance,
            CancellationToken.None);

        var run = await dbA.RestoreVerificationRuns.AsNoTracking().SingleAsync();
        Assert.Equal(RestoreVerificationStatus.Failed, run.Status);
        Assert.Equal("PRIOR", run.FailureCode);
    }
}
