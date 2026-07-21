using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// RKSV restore compliance gate for Super Admin validation-only restores.
/// Does <strong>not</strong> execute production <c>pg_restore</c> or rewrite fiscal timestamps.
/// Same-tenant rule, audit payload helpers, and explicit rejection of non-validation restores.
/// </summary>
public interface IRestoreService
{
    /// <summary>
    /// RKSV Rule 1: when both the backup run and the operating tenant context are set,
    /// they must match. Deployment-wide dumps (<c>backup.TenantId == null</c>) remain
    /// allowed for platform Super Admin drills.
    /// </summary>
    RestoreResult EvaluateSameTenant(Guid? backupTenantId, Guid? operatingTenantId);

    /// <summary>
    /// Full pre-flight for starting a validation restore request (Rules 1–3).
    /// Rule 3 (no backdating) is guaranteed by not rewriting fiscal columns — documented in result notes.
    /// </summary>
    RestoreResult EnsureCanStartValidationRestore(
        BackupRun backup,
        Guid? operatingTenantId,
        bool validationOnly);
}

/// <summary>
/// Default RKSV restore compliance implementation. Production restore remains deferred
/// (<see cref="IRestoreOrchestrationBoundary"/>); this service only gates the existing
/// dual-approval validation path in <c>ManualRestoreTriggerService</c>.
/// </summary>
public sealed class RestoreService : IRestoreService
{
    public const string CrossTenantCode = "CROSS_TENANT_RESTORE_FORBIDDEN";
    public const string ProductionRestoreCode = "PRODUCTION_RESTORE_NOT_SUPPORTED";
    public const string BackupNotSucceededCode = "BACKUP_NOT_SUCCEEDED";

    public RestoreResult EvaluateSameTenant(Guid? backupTenantId, Guid? operatingTenantId)
    {
        // RKSV Rule 1: same tenant only when both sides are known.
        if (backupTenantId is Guid backupTid && backupTid != Guid.Empty
            && operatingTenantId is Guid opTid && opTid != Guid.Empty
            && backupTid != opTid)
        {
            return RestoreResult.Fail(
                CrossTenantCode,
                "Cross-tenant restore is not allowed for RKSV compliance.");
        }

        return RestoreResult.Success();
    }

    public RestoreResult EnsureCanStartValidationRestore(
        BackupRun backup,
        Guid? operatingTenantId,
        bool validationOnly)
    {
        // Never allow a non-validation path through this gate (AGENTS.md: no auto production restore).
        if (!validationOnly)
        {
            return RestoreResult.Fail(
                ProductionRestoreCode,
                "ValidationOnly must be true; production restore is not supported.");
        }

        // Tenant JSON ZIP is not a pg_restore input (content policy / restore-boundary-notes).
        if (backup.Strategy == BackupStrategyKind.Tenant)
        {
            return RestoreResult.Fail(
                ComplianceCheckService.TenantPackageRestoreCode,
                "Tenant JSON ZIP packages are not restored via pg_restore; use a System validation dump.");
        }

        if (backup.Status != BackupRunStatus.Succeeded)
        {
            return RestoreResult.Fail(
                BackupNotSucceededCode,
                $"Backup run {backup.Id} must be in Succeeded status (current: {backup.Status}).");
        }

        // RKSV Rule 3: restored clone keeps original fiscal timestamps — enforced by not rewriting them.
        // Rule 2/4 (audit start/complete) are owned by ManualRestoreAudit at request/approve/outcome.

        return EvaluateSameTenant(backup.TenantId, operatingTenantId);
    }
}
