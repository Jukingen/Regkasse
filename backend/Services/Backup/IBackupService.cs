namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Backup orchestration facade with separate TenantAdmin and SuperAdmin strategies.
/// Create enqueues work on the existing worker; restore starts a validation-only dual-approval request
/// (never production <c>pg_restore</c>).
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Mandanten-Admin path: tenant-bound enqueue (Identity excluded, ~30d retention default).
    /// </summary>
    Task<BackupResult> CreateTenantBackupAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Super Admin path: deployment-wide enqueue (Identity included, ~90d retention default).
    /// </summary>
    Task<BackupResult> CreateSystemBackupAsync(
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// List backup runs with role-aware filtering.
    /// Super Admin: all runs. Mandanten-Admin: only <see cref="BackupStrategyKind.Tenant"/> for <paramref name="tenantId"/>.
    /// </summary>
    Task<BackupListResult> ListBackupsAsync(
        Guid? tenantId,
        bool isSuperAdmin,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Alias for <see cref="CreateTenantBackupAsync"/> (backward compatible).</summary>
    Task<BackupResult> CreateBackupAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// RKSV path: same-tenant check, dump integrity (SHA-256), then create a validation-only restore
    /// request pending second Super Admin approval. Does not write production.
    /// </summary>
    Task<RestoreResult> RestoreBackupAsync(
        Guid backupId,
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default);
}
