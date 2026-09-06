namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Caller context for deployment-wide <see cref="Models.Backup.BackupRun"/> read filters.
/// </summary>
public sealed record BackupRunAccessScope(
    bool IsSuperAdmin,
    Guid? CallerTenantId,
    string? CallerUserId)
{
    /// <summary>
    /// Super Admin always sees deployment-wide runs (including System dumps),
    /// even when the JWT carries an ambient tenant (FA default <c>dev</c>).
    /// </summary>
    public bool IsDeploymentWide => IsSuperAdmin;
}
