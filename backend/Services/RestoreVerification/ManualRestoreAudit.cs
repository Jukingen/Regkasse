using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>Typed audit trail for Super Admin validation-only manual restore workflow.</summary>
internal static class ManualRestoreAudit
{
    public static Task LogRequestedAsync(
        IAuditLogService audit,
        string actorUserId,
        ManualRestoreRequest entity,
        bool requiresApproval,
        string? correlationId,
        string? notes = null) =>
        audit.LogSystemOperationAsync(
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
                TargetDatabase = entity.TargetDatabaseName,
                RequiresApproval = requiresApproval,
                ValidationOnly = entity.ValidationOnly,
                Reason = entity.Reason
            },
            correlationIdOverride: correlationId,
            impersonationSnapshot: null,
            actionType: AuditEventType.RestoreRequested,
            entityId: entity.Id);

    public static Task LogApprovedAsync(
        IAuditLogService audit,
        string approverUserId,
        ManualRestoreRequest entity,
        Guid drillRunId,
        string? correlationId) =>
        audit.LogSystemOperationAsync(
            AuditLogActions.RESTORE_APPROVED,
            AuditLogEntityTypes.MANUAL_RESTORE_REQUEST,
            approverUserId,
            Roles.SuperAdmin,
            description: $"Manual restore request {entity.Id} approved; validation drill {drillRunId} queued.",
            requestData: new
            {
                RequestId = entity.Id,
                BackupRunId = entity.BackupRunId,
                TargetDatabase = entity.TargetDatabaseName,
                RestoreVerificationRunId = drillRunId,
                Reason = entity.Reason
            },
            correlationIdOverride: correlationId,
            impersonationSnapshot: null,
            actionType: AuditEventType.RestoreApproved,
            entityId: entity.Id);

    public static Task LogRejectedAsync(
        IAuditLogService audit,
        string actorUserId,
        ManualRestoreRequest entity,
        string rejectionReason,
        string? correlationId) =>
        audit.LogSystemOperationAsync(
            AuditLogActions.RESTORE_REJECTED,
            AuditLogEntityTypes.MANUAL_RESTORE_REQUEST,
            actorUserId,
            Roles.SuperAdmin,
            description: $"Manual restore request {entity.Id} rejected.",
            requestData: new
            {
                RequestId = entity.Id,
                BackupRunId = entity.BackupRunId,
                Reason = rejectionReason
            },
            correlationIdOverride: correlationId,
            impersonationSnapshot: null,
            actionType: AuditEventType.RestoreRejected,
            entityId: entity.Id);

    public static Task LogExecutionOutcomeAsync(
        IAuditLogService audit,
        string actorUserId,
        ManualRestoreRequest entity,
        bool succeeded,
        string? correlationId)
    {
        var action = succeeded ? AuditLogActions.RESTORE_COMPLETED : AuditLogActions.RESTORE_FAILED;
        var eventType = succeeded ? AuditEventType.RestoreCompleted : AuditEventType.RestoreFailed;
        var status = succeeded ? AuditLogStatus.Success : AuditLogStatus.Failed;
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
                TargetDatabase = entity.TargetDatabaseName,
                RestoreVerificationRunId = entity.RestoreVerificationRunId,
                Result = entity.Result
            },
            correlationIdOverride: correlationId,
            impersonationSnapshot: null,
            actionType: eventType,
            entityId: entity.Id);
    }
}
