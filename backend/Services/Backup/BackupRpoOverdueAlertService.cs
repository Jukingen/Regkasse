using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Alerts when no successful System strategy backup completed within <see cref="BackupOptions.AlertOnNoBackupDays"/>.
/// Gated by <see cref="BackupOptions.RpoOverdueAlertEnabled"/> and scheduled System backup being active.
/// </summary>
public sealed class BackupRpoOverdueAlertService : BackgroundService
{
    private static readonly TimeSpan MinCheckInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IBackupAlertPublisher _alerts;
    private readonly ILogger<BackupRpoOverdueAlertService> _logger;
    private DateTime? _lastPublishedAtUtc;

    public BackupRpoOverdueAlertService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupOptions> options,
        IBackupAlertPublisher alerts,
        ILogger<BackupRpoOverdueAlertService> logger)
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
                await CheckRpoAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup RPO overdue check failed");
            }

            var delay = _options.CurrentValue.RpoOverdueAlertCheckInterval;
            if (delay < MinCheckInterval)
                delay = MinCheckInterval;

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
    internal async Task CheckRpoAsync(CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        if (!opts.RpoOverdueAlertEnabled)
            return;

        // Avoid false positives when System scheduled backup is not intended to run.
        if (!opts.WorkerEnabled || !opts.ScheduledBackupEnabled)
            return;

        if (string.IsNullOrWhiteSpace(opts.GetEffectiveScheduledBackupCronExpression()))
            return;

        var days = opts.AlertOnNoBackupDays <= 0 ? 2 : opts.AlertOnNoBackupDays;
        var cutoff = DateTime.UtcNow.AddDays(-days);

        DateTime? lastCompletedAt;
        Guid? lastRunId;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var latest = await db.BackupRuns.AsNoTracking()
                .Where(r => r.Strategy == BackupStrategyKind.System
                            && r.Status == BackupRunStatus.Succeeded
                            && r.CompletedAt != null)
                .OrderByDescending(r => r.CompletedAt)
                .Select(r => new { r.Id, r.CompletedAt })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            lastCompletedAt = latest?.CompletedAt;
            lastRunId = latest?.Id;
        }

        if (lastCompletedAt != null && lastCompletedAt >= cutoff)
            return;

        var minInterval = opts.RpoOverdueAlertMinInterval;
        if (minInterval < TimeSpan.FromHours(1))
            minInterval = TimeSpan.FromHours(1);

        var now = DateTime.UtcNow;
        if (_lastPublishedAtUtc != null && now - _lastPublishedAtUtc.Value < minInterval)
            return;

        var ageText = lastCompletedAt == null
            ? "no successful system backup on record"
            : $"last successful system backup at {lastCompletedAt:O}";

        var message =
            $"No successful system backup for {days} days ({ageText}). RPO may be at risk.";

        _alerts.Publish(new BackupAlertEvent(
            BackupAlertKind.RpoOverdue,
            lastRunId,
            CorrelationId: null,
            Message: message,
            Data: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reason"] = "rpo_overdue",
                ["alertOnNoBackupDays"] = days.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["lastSuccessfulCompletedAtUtc"] = lastCompletedAt?.ToString("O") ?? string.Empty,
            }));

        _lastPublishedAtUtc = now;
        _logger.LogWarning("Published RPO overdue alert: {Message}", message);
    }
}
