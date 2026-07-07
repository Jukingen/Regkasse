namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Caller context for deployment-wide <see cref="Models.Backup.BackupRun"/> read filters.
/// </summary>
public sealed record BackupRunAccessScope(
    bool IsSuperAdmin,
    Guid? CallerTenantId,
    string? CallerUserId)
{
    public bool IsDeploymentWide => IsSuperAdmin && !CallerTenantId.HasValue;
}
