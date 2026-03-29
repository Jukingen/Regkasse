using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
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

    private static (BackupRecoverabilitySummaryService Svc, AppDbContext Db) CreateSut(
        string dbName,
        DateTime utcNow)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var db = new AppDbContext(options);
        var time = new FixedUtcTimeProvider(utcNow);
        var svc = new BackupRecoverabilitySummaryService(db, time);
        return (svc, db);
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
            LastSuccessfulArtifactVerificationAt = DateTime.UtcNow,
            LastSuccessfulRestoreProofAt = null,
            LastSuccessfulRestoreProofRunId = null,
            BackupProofAgeSeconds = 1,
            RestoreProofAgeSeconds = null,
            LatestRunAt = DateTime.UtcNow,
            LatestRunStatus = BackupRunStatus.Running,
            LatestRestoreRunAt = null,
            LatestRestoreRunStatus = null
        };
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("lastSuccessfulBackupAt", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulBackupRunId", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulArtifactVerificationAt", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulRestoreProofAt", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulRestoreProofRunId", out _));
        Assert.True(root.TryGetProperty("backupProofAgeSeconds", out _));
        Assert.True(root.TryGetProperty("restoreProofAgeSeconds", out _));
        Assert.True(root.TryGetProperty("latestRunAt", out _));
        Assert.True(root.TryGetProperty("latestRunStatus", out _));
        Assert.True(root.TryGetProperty("latestRestoreRunAt", out _));
        Assert.True(root.TryGetProperty("latestRestoreRunStatus", out _));
    }
}
