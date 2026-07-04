namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Tenant-scoped access checks for deployment-wide <see cref="Models.Backup.BackupRun"/> rows
/// (tenant is encoded in <c>idempotency_key</c> until <c>tenant_id</c> exists on the table).
/// </summary>
public interface IBackupRunTenantAccessService
{
    /// <summary>
    /// Returns the run when accessible; otherwise <c>null</c> (caller should respond with HTTP 404).
    /// Super Admin without tenant context may access any run; scoped callers only matching tenant hints.
    /// </summary>
    Task<Models.Backup.BackupRun?> TryGetAccessibleRunAsync(
        Guid backupRunId,
        bool isSuperAdmin,
        Guid? callerTenantId,
        CancellationToken cancellationToken = default);
}
