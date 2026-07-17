using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Typed audit trail for Super Admin validation-only manual restore workflow.
/// Always stamps an explicit audit <c>tenantId</c> (source backup tenant when set; otherwise
/// <see cref="LegacyDefaultTenantIds.Primary"/> as the platform/system convention) so ambient
/// impersonation cannot mis-attribute restore events.
/// </summary>
internal static class ManualRestoreAudit
{
    /// <summary>
    /// Platform restore / deployment-wide dump: audit row uses the legacy system tenant id
    /// (non-nullable <c>audit_logs.tenant_id</c>) while <c>requestData.SourceBackupTenantId</c> stays null.
    /// </summary>
    public static Guid ResolveRestoreAuditTenantId(Guid? sourceBackupTenantId) =>
        sourceBackupTenantId is Guid tid && tid != Guid.Empty
            ? tid
            : LegacyDefaultTenantIds.Primary;

    public static string ResolveRestoreScope(Guid? sourceBackupTenantId) =>
        sourceBackupTenantId is Guid tid && tid != Guid.Empty
            ? "tenant_access_scoped"
            : "deployment_wide";

    public static Task LogRequestedAsync(
        IAuditLogService audit,
        string actorUserId,
        ManualRestoreRequest entity,
        Guid? sourceBackupTenantId,
        bool requiresApproval,
        string? correlationId,
        string? notes = null)
    {
        var recordedAtUtc = DateTime.UtcNow;
        return audit.LogSystemOperationAsync(
            AuditLogActions.RESTORE_REQUESTED,
            AuditLogEntityTypes.MANUAL_RESTORE_REQUEST,
            actorUserId,
            Roles.SuperAdmin,
            description: $"Manual restore request {entity.Id} pending approval (validation-only).",
            notes: notes,
            requestData: new
            {
                RequestId = entity.Id,
                BackupRunId = entity.BackupRunId,
                SourceBackupTenantId = sourceBackupTenantId,
                RestoreScope = ResolveRestoreScope(sourceBackupTenantId),
                TargetDatabase = entity.TargetDatabaseName,
                RequiresApproval = requiresApproval,
                ValidationOnly = entity.ValidationOnly,
                Reason = entity.Reason,
                RequestedAtUtc = entity.RequestedAt,
                AuditRecordedAtUtc = recordedAtUtc,
                RksvCompliance = RksvComplianceNotes
            },
            correlationIdOverride: correlationId,
            impersonationSnapshot: null,
            actionType: AuditEventType.RestoreRequested,
            entityId: entity.Id,
            tenantId: ResolveRestoreAuditTenantId(sourceBackupTenantId));
    }

    public static Task LogApprovedAsync(
        IAuditLogService audit,
        string approverUserId,
        ManualRestoreRequest entity,
        Guid? sourceBackupTenantId,
        Guid drillRunId,
        string? correlationId)
    {
        var recordedAtUtc = DateTime.UtcNow;
        return audit.LogSystemOperationAsync(
            AuditLogActions.RESTORE_APPROVED,
            AuditLogEntityTypes.MANUAL_RESTORE_REQUEST,
            approverUserId,
            Roles.SuperAdmin,
            description: $"Manual restore request {entity.Id} approved; validation drill {drillRunId} queued.",
            requestData: new
            {
                RequestId = entity.Id,
                BackupRunId = entity.BackupRunId,
                SourceBackupTenantId = sourceBackupTenantId,
                RestoreScope = ResolveRestoreScope(sourceBackupTenantId),
                TargetDatabase = entity.TargetDatabaseName,
                RestoreVerificationRunId = drillRunId,
                Reason = entity.Reason,
                ApprovedAtUtc = entity.ApprovedAt,
                AuditRecordedAtUtc = recordedAtUtc,
                RksvCompliance = RksvComplianceNotes
            },
            correlationIdOverride: correlationId,
            impersonationSnapshot: null,
            actionType: AuditEventType.RestoreApproved,
            entityId: entity.Id,
            tenantId: ResolveRestoreAuditTenantId(sourceBackupTenantId));
    }

    public static Task LogRejectedAsync(
        IAuditLogService audit,
        string actorUserId,
        ManualRestoreRequest entity,
        Guid? sourceBackupTenantId,
        string rejectionReason,
        string? correlationId)
    {
        var recordedAtUtc = DateTime.UtcNow;
        return audit.LogSystemOperationAsync(
            AuditLogActions.RESTORE_REJECTED,
            AuditLogEntityTypes.MANUAL_RESTORE_REQUEST,
            actorUserId,
            Roles.SuperAdmin,
            description: $"Manual restore request {entity.Id} rejected.",
            requestData: new
            {
                RequestId = entity.Id,
                BackupRunId = entity.BackupRunId,
                SourceBackupTenantId = sourceBackupTenantId,
                RestoreScope = ResolveRestoreScope(sourceBackupTenantId),
                Reason = rejectionReason,
                RejectedAtUtc = entity.ApprovedAt,
                AuditRecordedAtUtc = recordedAtUtc,
                RksvCompliance = RksvComplianceNotes
            },
            correlationIdOverride: correlationId,
            impersonationSnapshot: null,
            actionType: AuditEventType.RestoreRejected,
            entityId: entity.Id,
            tenantId: ResolveRestoreAuditTenantId(sourceBackupTenantId));
    }

    public static Task LogExecutionOutcomeAsync(
        IAuditLogService audit,
        string actorUserId,
        ManualRestoreRequest entity,
        Guid? sourceBackupTenantId,
        bool succeeded,
        string? correlationId)
    {
        var action = succeeded ? AuditLogActions.RESTORE_COMPLETED : AuditLogActions.RESTORE_FAILED;
        var eventType = succeeded ? AuditEventType.RestoreCompleted : AuditEventType.RestoreFailed;
        var status = succeeded ? AuditLogStatus.Success : AuditLogStatus.Failed;
        var recordedAtUtc = DateTime.UtcNow;
        return audit.LogSystemOperationAsync(
            action,
            AuditLogEntityTypes.MANUAL_RESTORE_REQUEST,
            actorUserId,
            Roles.SuperAdmin,
            description: succeeded
                ? $"Manual restore request {entity.Id} completed (validation-only)."
                : $"Manual restore request {entity.Id} failed (validation-only).",
            status: status,
            requestData: new
            {
                RequestId = entity.Id,
                BackupRunId = entity.BackupRunId,
                SourceBackupTenantId = sourceBackupTenantId,
                RestoreScope = ResolveRestoreScope(sourceBackupTenantId),
                TargetDatabase = entity.TargetDatabaseName,
                RestoreVerificationRunId = entity.RestoreVerificationRunId,
                Result = entity.Result,
                CompletedAtUtc = recordedAtUtc,
                AuditRecordedAtUtc = recordedAtUtc,
                RksvCompliance = RksvComplianceNotes
            },
            correlationIdOverride: correlationId,
            impersonationSnapshot: null,
            actionType: eventType,
            entityId: entity.Id,
            tenantId: ResolveRestoreAuditTenantId(sourceBackupTenantId));
    }

    /// <summary>
    /// RKSV-oriented restore guardrails recorded on every manual restore audit event.
    /// Production DB is never written; fiscal timestamps in restored clones are not rewritten.
    /// </summary>
    private const string RksvComplianceNotes =
        "validation_only;isolated_target;no_production_write;no_fiscal_timestamp_rewrite;dual_superadmin_approval";
}
