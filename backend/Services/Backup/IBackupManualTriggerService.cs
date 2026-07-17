using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupManualTriggerService
{
    /// <summary>Enqueues a backup run. Does not execute backup work on the caller thread.</summary>
    /// <param name="strategy">
    /// When null, resolved from tenant scope (tenant present → Tenant, else System).
    /// </param>
    /// <param name="deploymentWide">
    /// When true, forces System strategy and <c>tenant_id</c> null (Super Admin system backup),
    /// ignoring ambient tenant for the run row.
    /// </param>
    /// <param name="incrementalSinceUtc">
    /// When set (Tenant strategy only), marks the run as an incremental package watermark for the worker exporter.
    /// </param>
    Task<BackupManualTriggerOutcome> RequestManualBackupAsync(
        string? requestedByUserId,
        string requestedByRole,
        string? idempotencyKey,
        string? correlationId,
        BackupStrategyKind? strategy = null,
        bool deploymentWide = false,
        CancellationToken cancellationToken = default,
        DateTime? incrementalSinceUtc = null);
}
