using System.Collections.Generic;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Recoverability gauge yenileme ve zamanlanmış kanıt / yapılandırma risk uyarıları (dedupe ile).
/// </summary>
public sealed class DrOperationalObservabilityHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OperationalDrObservabilityOptions> _obsOptions;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _restoreOptions;
    private readonly IDrOperationalObservabilityMetrics _metrics;
    private readonly IRestoreVerificationOperationalReadiness _restoreReadiness;
    private readonly IBackupAlertPublisher _alerts;
    private readonly ILogger<DrOperationalObservabilityHostedService> _logger;

    private DateTimeOffset? _lastProofCadenceAlertUtc;
    private DateTimeOffset? _lastUnhealthyConfigAlertUtc;
    private DateTimeOffset? _lastWorkerDisabledAlertUtc;

    public DrOperationalObservabilityHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OperationalDrObservabilityOptions> obsOptions,
        IOptionsMonitor<RestoreVerificationOptions> restoreOptions,
        IDrOperationalObservabilityMetrics metrics,
        IRestoreVerificationOperationalReadiness restoreReadiness,
        IBackupAlertPublisher alerts,
        ILogger<DrOperationalObservabilityHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _obsOptions = obsOptions;
        _restoreOptions = restoreOptions;
        _metrics = metrics;
        _restoreReadiness = restoreReadiness;
        _alerts = alerts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = _obsOptions.CurrentValue.RecoverabilityRefreshInterval;
            if (delay < TimeSpan.FromSeconds(15))
                delay = TimeSpan.FromSeconds(15);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var sp = scope.ServiceProvider;
                var summary = await sp.GetRequiredService<IBackupRecoverabilitySummaryService>()
                    .GetAsync(accessScope: null, stoppingToken);

                _metrics.SetRecoverabilityProofAgeSeconds("backup", summary.BackupProofAgeSeconds);
                _metrics.SetRecoverabilityProofAgeSeconds("restore", summary.RestoreProofAgeSeconds);

                var ro = _restoreOptions.CurrentValue;
                var obs = _obsOptions.CurrentValue;
                EvaluateRestoreDrillRisks(summary, ro, obs);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DR observability refresh tick failed");
            }

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

    private void EvaluateRestoreDrillRisks(
        BackupRecoverabilitySummaryResponseDto summary,
        RestoreVerificationOptions ro,
        OperationalDrObservabilityOptions obs)
    {
        if (ro.ScheduledWeeklyDrillEnabled
            && !ro.WorkerEnabled
            && obs.EmitWorkerDisabledScheduledDrillAlerts
            && ShouldAlert(obs.WorkerDisabledScheduledDrillAlertMinInterval, ref _lastWorkerDisabledAlertUtc))
        {
            _alerts.Publish(new BackupAlertEvent(
                BackupAlertKind.RestoreDrillOperationalRisk,
                null,
                null,
                "Scheduled weekly restore verification is enabled but WorkerEnabled=false; queued drills will not run.",
                new Dictionary<string, string> { ["reason"] = "worker_disabled_with_scheduled_drill" }));
        }

        var health = _restoreReadiness.GetConfigurationHealth();
        if (health.Level == RestoreVerificationConfigurationHealthLevel.Unhealthy
            && obs.EmitUnhealthyRestoreConfigAlerts
            && ShouldAlert(obs.UnhealthyRestoreConfigAlertMinInterval, ref _lastUnhealthyConfigAlertUtc))
        {
            _alerts.Publish(new BackupAlertEvent(
                BackupAlertKind.RestoreDrillOperationalRisk,
                null,
                null,
                "Restore verification configuration is Unhealthy; review polling interval and distributed lock settings.",
                new Dictionary<string, string>
                {
                    ["reason"] = "unhealthy_restore_configuration",
                    ["issues"] = string.Join(" | ", health.Issues)
                }));
        }

        if (!ro.ScheduledWeeklyDrillEnabled || !obs.EmitProofCadenceRiskAlerts)
            return;

        var maxAgeSec = (double)ro.ScheduledProofCadenceDays * 86400.0;
        var overdue = !summary.RestoreProofAgeSeconds.HasValue
                      || summary.RestoreProofAgeSeconds.Value > maxAgeSec;

        if (overdue
            && ShouldAlert(obs.ProofCadenceRiskAlertMinInterval, ref _lastProofCadenceAlertUtc))
        {
            var age = summary.RestoreProofAgeSeconds;
            _alerts.Publish(new BackupAlertEvent(
                BackupAlertKind.RestoreDrillOperationalRisk,
                null,
                null,
                "Restore verification proof is older than configured cadence or missing while scheduled drills are enabled.",
                new Dictionary<string, string>
                {
                    ["reason"] = "restore_proof_overdue_or_missing",
                    ["scheduledProofCadenceDays"] = ro.ScheduledProofCadenceDays.ToString(),
                    ["restoreProofAgeSeconds"] = age?.ToString() ?? "null",
                    ["lastSuccessfulRestoreProofRunId"] = summary.LastSuccessfulRestoreProofRunId?.ToString() ?? ""
                },
                summary.LastSuccessfulRestoreProofRunId));
        }
    }

    private static bool ShouldAlert(TimeSpan minInterval, ref DateTimeOffset? lastUtc)
    {
        var now = DateTimeOffset.UtcNow;
        if (!lastUtc.HasValue || now - lastUtc.Value >= minInterval)
        {
            lastUtc = now;
            return true;
        }

        return false;
    }
}
