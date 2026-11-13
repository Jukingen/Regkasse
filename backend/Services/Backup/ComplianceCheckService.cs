using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Pre-restore compliance: same-tenant, logical-dump integrity, RKSV validation-only gates.
/// Uses <see cref="BackupRun"/> (there is no <c>Backups</c> table). Never hard-codes success.
/// </summary>
public sealed class ComplianceCheckService : IComplianceCheckService
{
    public const string BackupNotFoundCode = "BACKUP_NOT_FOUND";
    public const string IntegrityFailedCode = "BACKUP_INTEGRITY_FAILED";
    public const string IntegrityHashMissingCode = "BACKUP_HASH_MISSING";
    public const string TenantPackageRestoreCode = "TENANT_PACKAGE_RESTORE_NOT_SUPPORTED";

    public const string CheckSameTenant = "SameTenant";
    public const string CheckBackupIntegrity = "BackupIntegrity";
    public const string CheckRksvGate = "RksvValidationGate";
    public const string CheckArtifactStrategy = "ArtifactStrategy";

    private readonly AppDbContext _db;
    private readonly IRestoreService _restoreService;
    private readonly IBackupChecksumService _checksum;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ILogger<ComplianceCheckService> _logger;

    public ComplianceCheckService(
        AppDbContext db,
        IRestoreService restoreService,
        IBackupChecksumService checksum,
        IOptionsMonitor<BackupOptions> options,
        ILogger<ComplianceCheckService> logger)
    {
        _db = db;
        _restoreService = restoreService;
        _checksum = checksum;
        _options = options;
        _logger = logger;
    }

    public async Task<ComplianceResult> CheckRestoreComplianceAsync(
        Guid backupId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var checks = new List<ComplianceCheckItem>();
        var operatingTenantId = tenantId == Guid.Empty ? null : (Guid?)tenantId;

        var backup = await _db.BackupRuns.AsNoTracking()
            .Include(r => r.Artifacts)
            .FirstOrDefaultAsync(b => b.Id == backupId, ct);

        if (backup is null)
        {
            checks.Add(ComplianceCheckItem.Fail(CheckRksvGate, $"Backup run {backupId} was not found."));
            return ComplianceResult.Fail(
                BackupNotFoundCode,
                $"Backup run {backupId} was not found.",
                checks: checks);
        }

        // ✅ Check 1: Same tenant (when both sides are known)
        var sameTenant = _restoreService.EvaluateSameTenant(backup.TenantId, operatingTenantId);
        if (!sameTenant.Succeeded)
        {
            checks.Add(ComplianceCheckItem.Fail(
                CheckSameTenant,
                sameTenant.Error ?? "Cross-tenant restore is not allowed for RKSV compliance."));
            return ComplianceResult.Fail(
                sameTenant.Code ?? RestoreService.CrossTenantCode,
                sameTenant.Error ?? "Cross-tenant restore is not allowed for RKSV compliance.",
                backup.Id,
                backup.TenantId,
                checks);
        }

        checks.Add(ComplianceCheckItem.Pass(
            CheckSameTenant,
            backup.TenantId is null
                ? "deployment_wide_backup_allowed"
                : "same_tenant_ok"));

        // ✅ Check 2: Backup integrity (SHA-256 of logical dump when file is present)
        var integrity = await VerifyBackupIntegrityAsync(backup, ct);
        if (!integrity.Succeeded)
        {
            checks.Add(ComplianceCheckItem.Fail(
                CheckBackupIntegrity,
                integrity.Error ?? "Backup integrity check failed."));
            return ComplianceResult.Fail(
                integrity.Code ?? IntegrityFailedCode,
                integrity.Error ?? "Backup integrity check failed.",
                backup.Id,
                backup.TenantId,
                checks);
        }

        checks.Add(ComplianceCheckItem.Pass(
            CheckBackupIntegrity,
            integrity.Detail ?? "integrity_ok"));

        // ✅ Check 3: RKSV / content policy — Tenant ZIP is not a pg_restore input
        if (backup.Strategy == BackupStrategyKind.Tenant)
        {
            const string msg =
                "Tenant JSON ZIP packages are not restored via pg_restore; use a System validation dump for isolated restore drills.";
            checks.Add(ComplianceCheckItem.Fail(CheckArtifactStrategy, msg));
            return ComplianceResult.Fail(
                TenantPackageRestoreCode,
                msg,
                backup.Id,
                backup.TenantId,
                checks);
        }

        checks.Add(ComplianceCheckItem.Pass(CheckArtifactStrategy, "system_or_compatible_artifact"));

        // ✅ Check 3b: RKSV validation gate (Succeeded + validation-only + same-tenant already covered)
        var rksv = _restoreService.EnsureCanStartValidationRestore(
            backup,
            operatingTenantId,
            validationOnly: true);
        if (!rksv.Succeeded)
        {
            checks.Add(ComplianceCheckItem.Fail(
                CheckRksvGate,
                rksv.Error ?? "RKSV compliance check failed."));
            return ComplianceResult.Fail(
                rksv.Code ?? "RKSV_COMPLIANCE_FAILED",
                rksv.Error ?? "RKSV compliance check failed.",
                backup.Id,
                backup.TenantId,
                checks);
        }

        checks.Add(ComplianceCheckItem.Pass(
            CheckRksvGate,
            "validation_only;no_fiscal_timestamp_rewrite;succeeded_backup"));

        return ComplianceResult.Success(backup.Id, backup.TenantId, checks);
    }

    private async Task<(bool Succeeded, string? Code, string? Error, string? Detail)> VerifyBackupIntegrityAsync(
        BackupRun backup,
        CancellationToken ct)
    {
        var dump = backup.Artifacts
            .Where(a => a.ArtifactType == BackupArtifactType.LogicalDump)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (dump is null)
        {
            return (false, IntegrityFailedCode, "Backup has no logical dump artifact to verify.", null);
        }

        if (string.IsNullOrWhiteSpace(dump.ContentHashSha256) || dump.ContentHashSha256.Length != 64)
        {
            return (
                false,
                IntegrityHashMissingCode,
                "Backup dump is missing a valid SHA-256 content hash.",
                null);
        }

        var root = _options.CurrentValue.ArtifactStagingRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            return (true, null, null, "hash_present_staging_root_unset");
        }

        var rootFull = Path.GetFullPath(root.Trim());
        if (!BackupArtifactPathResolver.TryResolveStagingAbsolute(rootFull, dump.StorageDescriptor, out var fullPath)
            || string.IsNullOrWhiteSpace(fullPath)
            || !File.Exists(fullPath))
        {
            _logger.LogWarning(
                "Compliance integrity: dump file not under staging for run {RunId}; relying on stored SHA-256.",
                backup.Id);
            return (true, null, null, "hash_present_file_not_in_staging");
        }

        var matches = await _checksum.FileMatchesSha256Async(fullPath, dump.ContentHashSha256, ct);
        if (!matches)
        {
            return (
                false,
                IntegrityFailedCode,
                "Logical dump SHA-256 does not match stored content hash.",
                null);
        }

        return (true, null, null, "sha256_verified_on_disk");
    }
}
