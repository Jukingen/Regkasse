namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Pre-flight RKSV compliance checks before starting a validation-only restore request.
/// </summary>
public interface IComplianceCheckService
{
    /// <summary>
    /// Check 1 same-tenant, Check 2 dump integrity (SHA-256), Check 3 RKSV validation gates
    /// (Succeeded backup, validation-only, no Tenant ZIP as pg_restore input).
    /// <paramref name="tenantId"/> of <see cref="Guid.Empty"/> means no ambient operating tenant (platform Super Admin).
    /// </summary>
    Task<ComplianceResult> CheckRestoreComplianceAsync(
        Guid backupId,
        Guid tenantId,
        CancellationToken ct = default);
}
