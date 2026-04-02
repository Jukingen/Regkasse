using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRecoverabilitySummaryServiceTests
{
    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedUtcTimeProvider(DateTime utcInstant)
        {
            _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc));
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static BackupConfigurationHealthSnapshot DefaultConfigurationHealth() => new()
    {
        Level = BackupConfigurationHealthLevel.Healthy,
        Issues = Array.Empty<string>(),
        EffectiveAdapterKind = BackupExecutionAdapterKind.Fake,
        ConfigurationExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
        AdminRuntimeExecutionMode = AdminBackupRuntimeExecutionMode.InheritFromConfiguration,
        WorkerEnabled = true,
        RealPostgreSqlLogicalDumpConfigured = false,
        BackupExecutionReality = BackupConfigurationEvaluation.BackupExecutionRealitySimulatedFake,
        NonRealBackupAdapterAcknowledgmentConfigurationKey = null,
        ReadinessNarrative = "Development: backup adapter Fake does not perform production PostgreSQL logical dumps.",
        ArtifactVerificationDisclaimer = "test"
    };

    private static IBackupOperationalReadiness StubReadiness(BackupConfigurationHealthSnapshot? snap = null)
    {
        var m = new Mock<IBackupOperationalReadiness>();
        var effective = snap ?? DefaultConfigurationHealth();
        m.Setup(x => x.GetConfigurationHealth()).Returns(effective);
        m.Setup(x => x.GetConfigurationHealthAssumingAdminMode(It.IsAny<AdminBackupRuntimeExecutionMode>())).Returns(effective);
        return m.Object;
    }

    private static (BackupRecoverabilitySummaryService Svc, AppDbContext Db) CreateSut(
        string dbName,
        DateTime utcNow,
        IBackupOperationalReadiness? readiness = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var db = new AppDbContext(options);
        var time = new FixedUtcTimeProvider(utcNow);
        var svc = new BackupRecoverabilitySummaryService(db, time, readiness ?? StubReadiness());
        return (svc, db);
    }

    [Fact]
    public async Task GetAsync_embeds_backup_readiness_from_operational_readiness()
    {
        var snap = new BackupConfigurationHealthSnapshot
        {
            Level = BackupConfigurationHealthLevel.Degraded,
            Issues = new[] { "issue-a" },
            EffectiveAdapterKind = BackupExecutionAdapterKind.ProductionStub,
            ConfigurationExecutionAdapterKind = BackupExecutionAdapterKind.ProductionStub,
            AdminRuntimeExecutionMode = AdminBackupRuntimeExecutionMode.InheritFromConfiguration,
            WorkerEnabled = true,
            RealPostgreSqlLogicalDumpConfigured = false,
            BackupExecutionReality = BackupConfigurationEvaluation.BackupExecutionRealityProductionStubNoPostgreSql,
            NonRealBackupAdapterAcknowledgmentConfigurationKey = "Backup:AcknowledgePhase1NoRealBackup",
            ReadinessNarrative = "stub narrative line",
            ArtifactVerificationDisclaimer = "d"
        };
        var (svc, _) = CreateSut($"emb_{Guid.NewGuid():N}", DateTime.UtcNow, StubReadiness(snap));
        var s = await svc.GetAsync();
        Assert.Equal("stub narrative line", s.BackupReadinessNarrative);
        Assert.Equal("Degraded", s.BackupReadinessLevel);
        Assert.False(s.RealPostgreSqlLogicalDumpConfigured);
        Assert.Equal(BackupConfigurationEvaluation.BackupExecutionRealityProductionStubNoPostgreSql, s.BackupExecutionReality);
    }

    [Fact]
    public async Task Latest_backup_failed_but_previous_successful_shows_both_distinctly()
    {
        var now = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        var dbName = $"recv_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName, now);
        var okId = Guid.NewGuid();

        db.BackupRuns.Add(new BackupRun
        {
            Id = okId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = now.AddHours(-2),
            CompletedAt = now.AddHours(-2)
        });
        db.BackupRuns.Add(new BackupRun
        {
            Status = BackupRunStatus.Failed,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = now.AddHours(-1),
            CompletedAt = now.AddHours(-1),
            FailureCode = "X"
        });
        await db.SaveChangesAsync();

        var summary = await svc.GetAsync();

        Assert.Equal(BackupRunStatus.Failed, summary.LatestRunStatus);
        Assert.Equal(now.AddHours(-1), summary.LatestRunAt);

        Assert.Equal(okId, summary.LastSuccessfulBackupRunId);
        Assert.Equal(now.AddHours(-2), summary.LastSuccessfulBackupAt);
        Assert.Equal(7200L, summary.BackupProofAgeSeconds);
        Assert.True(summary.LastSuccessfulBackupRunIsSimulatedExecution);
    }

    [Fact]
    public async Task GetAsync_last_successful_backup_simulated_flag_false_for_pg_dump_adapter()
    {
        var now = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        var dbName = $"recv_pg_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName, now);
        db.BackupRuns.Add(new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = nameof(BackupExecutionAdapterKind.PgDump),
            RequestedAt = now.AddHours(-1),
            CompletedAt = now.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var summary = await svc.GetAsync();

        Assert.False(summary.LastSuccessfulBackupRunIsSimulatedExecution);
    }

    [Fact]
    public async Task Manual_restore_success_does_not_populate_scheduled_restore_proof_fields()
    {
        var now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var dbName = $"recv_rv_manual_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName, now);

        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = now.AddMinutes(-10),
            CompletedAt = now.AddMinutes(-10)
        });
        await db.SaveChangesAsync();

        var summary = await svc.GetAsync();

        Assert.Null(summary.LastSuccessfulRestoreProofAt);
        Assert.Null(summary.LastSuccessfulRestoreProofRunId);
        Assert.Null(summary.RestoreProofAgeSeconds);
    }

    [Fact]
    public async Task Scheduled_restore_success_populates_restore_proof_fields()
    {
        var now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var proofAt = now.AddMinutes(-20);
        var dbName = $"recv_rv_sched_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName, now);
        var id = Guid.NewGuid();

        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = id,
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = proofAt.AddMinutes(-5),
            CompletedAt = proofAt
        });
        await db.SaveChangesAsync();

        var summary = await svc.GetAsync();

        Assert.Equal(proofAt, summary.LastSuccessfulRestoreProofAt);
        Assert.Equal(id, summary.LastSuccessfulRestoreProofRunId);
        Assert.Equal(1200L, summary.RestoreProofAgeSeconds);
    }

    [Fact]
    public async Task No_restore_verification_rows_yields_null_restore_proof_fields()
    {
        var now = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var dbName = $"recv_rv_empty_{Guid.NewGuid():N}";
        var (svc, _) = CreateSut(dbName, now);

        var summary = await svc.GetAsync();

        Assert.Null(summary.LastSuccessfulRestoreProofAt);
        Assert.Null(summary.LastSuccessfulRestoreProofRunId);
        Assert.Null(summary.RestoreProofAgeSeconds);
        Assert.Null(summary.LatestRestoreRunAt);
        Assert.Null(summary.LatestRestoreRunStatus);
    }

    [Fact]
    public async Task Restore_proof_age_seconds_matches_fixed_clock()
    {
        var proofAt = new DateTime(2026, 4, 10, 9, 59, 30, DateTimeKind.Utc);
        var now = new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc);
        var dbName = $"recv_rv_age_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName, now);

        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = proofAt,
            CompletedAt = proofAt
        });
        await db.SaveChangesAsync();

        var summary = await svc.GetAsync();
        Assert.Equal(30L, summary.RestoreProofAgeSeconds);
    }

    [Fact]
    public async Task Last_successful_artifact_verification_distinct_from_latest_failed_backup()
    {
        var now = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc);
        var verifyAt = now.AddHours(-3);
        var dbName = $"recv_v_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName, now);
        var okId = Guid.NewGuid();

        db.BackupRuns.Add(new BackupRun
        {
            Id = okId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = now.AddHours(-4),
            CompletedAt = now.AddHours(-4)
        });
        db.BackupVerifications.Add(new BackupVerification
        {
            BackupRunId = okId,
            Status = BackupVerificationStatus.Passed,
            StartedAt = verifyAt,
            CompletedAt = verifyAt,
            VerifierSource = "ArtifactMetadataVerifier",
            CompletenessFlag = true
        });
        db.BackupRuns.Add(new BackupRun
        {
            Status = BackupRunStatus.Failed,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = now.AddHours(-1),
            CompletedAt = now.AddHours(-1),
            FailureCode = "X"
        });
        await db.SaveChangesAsync();

        var summary = await svc.GetAsync();

        Assert.Equal(BackupRunStatus.Failed, summary.LatestRunStatus);
        Assert.Equal(now.AddHours(-1), summary.LatestRunAt);
        Assert.Equal(okId, summary.LastSuccessfulBackupRunId);
        Assert.Equal(verifyAt, summary.LastSuccessfulArtifactVerificationAt);
    }

    [Fact]
    public async Task No_successful_restore_proof_yields_null_restore_fields()
    {
        var now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var dbName = $"recv_rv_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName, now);

        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Failed,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = now.AddMinutes(-30),
            CompletedAt = now.AddMinutes(-30),
            FailureCode = "NO_DUMP"
        });
        await db.SaveChangesAsync();

        var summary = await svc.GetAsync();

        Assert.Null(summary.LastSuccessfulRestoreProofAt);
        Assert.Null(summary.LastSuccessfulRestoreProofRunId);
        Assert.Null(summary.RestoreProofAgeSeconds);
        Assert.Equal(RestoreVerificationStatus.Failed, summary.LatestRestoreRunStatus);
        Assert.NotNull(summary.LatestRestoreRunAt);
    }

    [Fact]
    public async Task Backup_proof_age_matches_fixed_clock()
    {
        var proofAt = new DateTime(2026, 3, 29, 11, 58, 20, DateTimeKind.Utc);
        var now = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        var dbName = $"recv_age_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName, now);

        db.BackupRuns.Add(new BackupRun
        {
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "Fake",
            RequestedAt = proofAt,
            CompletedAt = proofAt
        });
        await db.SaveChangesAsync();

        var summary = await svc.GetAsync();
        Assert.Equal(100L, summary.BackupProofAgeSeconds);
    }

    [Fact]
    public void BackupLatestStatusResponseDto_contract_unchanged()
    {
        var expected = new[]
        {
            nameof(BackupLatestStatusResponseDto.ArtifactPipelinePolicy),
            nameof(BackupLatestStatusResponseDto.AverageSucceededBackupDurationSampleCount),
            nameof(BackupLatestStatusResponseDto.AverageSucceededBackupDurationSeconds),
            nameof(BackupLatestStatusResponseDto.ConfigurationHealth),
            nameof(BackupLatestStatusResponseDto.LatestRun),
            nameof(BackupLatestStatusResponseDto.Restore)
        };
        var actual = typeof(BackupLatestStatusResponseDto).GetProperties().Select(p => p.Name).OrderBy(x => x).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Recoverability_summary_serializes_expected_json_shape()
    {
        var dto = new BackupRecoverabilitySummaryResponseDto
        {
            LastSuccessfulBackupAt = DateTime.UtcNow,
            LastSuccessfulBackupRunId = Guid.NewGuid(),
            LastSuccessfulBackupRunIsSimulatedExecution = true,
            LastSuccessfulArtifactVerificationAt = DateTime.UtcNow,
            LastSuccessfulRestoreProofAt = null,
            LastSuccessfulRestoreProofRunId = null,
            BackupProofAgeSeconds = 1,
            RestoreProofAgeSeconds = null,
            LatestRunAt = DateTime.UtcNow,
            LatestRunStatus = BackupRunStatus.Running,
            LatestRestoreRunAt = null,
            LatestRestoreRunStatus = null,
            BackupExecutionReality = "SimulatedFake",
            RealPostgreSqlLogicalDumpConfigured = false,
            BackupReadinessLevel = "Healthy",
            BackupReadinessNarrative = "n"
        };
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("lastSuccessfulBackupAt", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulBackupRunId", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulBackupRunIsSimulatedExecution", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulArtifactVerificationAt", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulRestoreProofAt", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulRestoreProofRunId", out _));
        Assert.True(root.TryGetProperty("backupProofAgeSeconds", out _));
        Assert.True(root.TryGetProperty("restoreProofAgeSeconds", out _));
        Assert.True(root.TryGetProperty("latestRunAt", out _));
        Assert.True(root.TryGetProperty("latestRunStatus", out _));
        Assert.True(root.TryGetProperty("latestRestoreRunAt", out _));
        Assert.True(root.TryGetProperty("latestRestoreRunStatus", out _));
        Assert.True(root.TryGetProperty("backupExecutionReality", out _));
        Assert.True(root.TryGetProperty("realPostgreSqlLogicalDumpConfigured", out _));
        Assert.True(root.TryGetProperty("backupReadinessLevel", out _));
        Assert.True(root.TryGetProperty("backupReadinessNarrative", out _));
    }
}
