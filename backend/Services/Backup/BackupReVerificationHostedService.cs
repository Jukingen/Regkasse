using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Daily (configurable) on-disk SHA-256 re-verification of recent succeeded backups.
/// </summary>
public sealed class BackupReVerificationHostedService : BackgroundService
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupReVerificationOptions> _options;
    private readonly IBackupAlertPublisher _alerts;
    private readonly ILogger<BackupReVerificationHostedService> _logger;

    public BackupReVerificationHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupReVerificationOptions> options,
        IBackupAlertPublisher alerts,
        ILogger<BackupReVerificationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _alerts = alerts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup re-verification tick failed");
            }

            var delay = _options.CurrentValue.GetCheckInterval();
            if (delay < MinInterval)
                delay = MinInterval;

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>One monitoring tick (test hook via InternalsVisibleTo).</summary>
    internal async Task RunTickAsync(CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
            return;

        var retentionDays = opts.RetentionDays <= 0 ? 7 : opts.RetentionDays;
        var maxRuns = opts.MaxRunsPerTick <= 0 ? 20 : opts.MaxRunsPerTick;
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var interval = opts.GetCheckInterval();
        var recentVerifyCutoff = DateTime.UtcNow - interval;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifier = scope.ServiceProvider.GetRequiredService<IBackupChecksumVerificationService>();

        var candidateIds = await db.BackupRuns.AsNoTracking()
            .Where(r => r.Status == BackupRunStatus.Succeeded
                        && r.CompletedAt != null
                        && r.CompletedAt >= cutoff)
            .Where(r => !db.BackupVerifications.Any(v =>
                v.BackupRunId == r.Id
                && v.VerifierSource == IBackupChecksumVerificationService.VerifierSourceScheduledReverify
                && v.CompletedAt != null
                && v.CompletedAt >= recentVerifyCutoff))
            .OrderByDescending(r => r.CompletedAt)
            .Select(r => r.Id)
            .Take(maxRuns)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidateIds.Count == 0)
        {
            _logger.LogDebug("Backup re-verification: no candidate runs in retention window");
            return;
        }

        _logger.LogInformation(
            "Backup re-verification: verifying {Count} run(s) (retentionDays={RetentionDays})",
            candidateIds.Count,
            retentionDays);

        foreach (var runId in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await verifier.VerifyAndPersistAsync(
                        runId,
                        IBackupChecksumVerificationService.VerifierSourceScheduledReverify,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!result.IsValid)
                {
                    _alerts.Publish(new BackupAlertEvent(
                        BackupAlertKind.VerificationFailed,
                        runId,
                        CorrelationId: null,
                        Message: result.FailureReason
                                  ?? $"Scheduled checksum re-verification failed for backup {runId}.",
                        Data: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["reason"] = "scheduled_reverify_failed",
                            ["verifierSource"] = IBackupChecksumVerificationService.VerifierSourceScheduledReverify,
                        }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled checksum re-verification failed for run {BackupRunId}", runId);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.VerificationFailed,
                    runId,
                    CorrelationId: null,
                    Message: $"Scheduled checksum re-verification error for backup {runId}: {ex.Message}",
                    Data: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["reason"] = "scheduled_reverify_exception",
                    }));
            }
        }
    }
}
