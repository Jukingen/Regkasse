using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Publishes <see cref="ActivityEventType.BackupSucceeded"/> after a terminal successful backup run.</summary>
public sealed class BackupPostSuccessActivityHook : IBackupPostSuccessOrchestrationHook
{
    private readonly BackupPostSuccessOrchestrationHook _inner;
    private readonly ActivityEventRecorder _activity;
    private readonly ILogger<BackupPostSuccessActivityHook> _logger;

    public BackupPostSuccessActivityHook(
        BackupPostSuccessOrchestrationHook inner,
        ActivityEventRecorder activity,
        ILogger<BackupPostSuccessActivityHook> logger)
    {
        _inner = inner;
        _activity = activity;
        _logger = logger;
    }

    public async Task NotifySucceededAsync(AppDbContext db, BackupRun run, CancellationToken cancellationToken = default)
    {
        await _inner.NotifySucceededAsync(db, run, cancellationToken).ConfigureAwait(false);
        await TryPublishSucceededActivityAsync(db, run, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryPublishSucceededActivityAsync(
        AppDbContext db,
        BackupRun run,
        CancellationToken cancellationToken)
    {
        try
        {
            var durationSeconds = 0;
            if (run.StartedAt.HasValue && run.CompletedAt.HasValue)
                durationSeconds = Math.Max(0, (int)(run.CompletedAt.Value - run.StartedAt.Value).TotalSeconds);

            var artifactSize = await db.BackupArtifacts
                .AsNoTracking()
                .Where(a => a.BackupRunId == run.Id)
                .SumAsync(a => a.ByteSize ?? 0L, cancellationToken)
                .ConfigureAwait(false);

            await _activity.TryPublishAsync(
                LegacyDefaultTenantIds.Primary,
                ActivityEventType.BackupSucceeded,
                new
                {
                    BackupRunId = run.Id,
                    DurationSeconds = durationSeconds,
                    ArtifactSize = artifactSize,
                },
                actorUserId: run.RequestedByUserId,
                dedupKey: $"backup_succeeded_{run.Id}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish BackupSucceeded activity for run {RunId}", run.Id);
        }
    }
}
