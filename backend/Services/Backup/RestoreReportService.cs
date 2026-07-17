using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Generates compliance reports from <see cref="ManualRestoreRequest"/> (+ optional drill evidence).
/// Does not invent a RestoreHistory table or hard-code RKSV success.
/// </summary>
public sealed class RestoreReportService : IRestoreReportService
{
    public const string RksvComplianceNotes =
        "validation_only;isolated_target;no_production_write;no_fiscal_timestamp_rewrite;dual_superadmin_approval";

    private readonly AppDbContext _db;
    private readonly ManualRestoreTargetDatabaseGuard _targetGuard;

    public RestoreReportService(AppDbContext db, ManualRestoreTargetDatabaseGuard targetGuard)
    {
        _db = db;
        _targetGuard = targetGuard;
    }

    public async Task<RestoreReport?> GenerateRestoreReportAsync(
        Guid restoreId,
        CancellationToken ct = default)
    {
        var restore = await _db.ManualRestoreRequests
            .AsNoTracking()
            .Include(r => r.BackupRun)
            .Include(r => r.RestoreVerificationRun)
            .FirstOrDefaultAsync(r => r.Id == restoreId, ct);

        if (restore is null)
            return null;

        string? tenantName = null;
        var tenantId = restore.BackupRun?.TenantId;
        if (tenantId is Guid tid && tid != Guid.Empty)
        {
            tenantName = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tid)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct);
        }

        var findings = new List<string>();
        var processOk = EvaluateProcessCompliance(restore, findings);
        var outcomeOk = EvaluateOutcomeCompliance(restore, findings);
        var fiscalOk = EvaluateFiscalEvidence(restore.RestoreVerificationRun, findings);

        return new RestoreReport
        {
            RestoreId = restore.Id,
            TenantId = tenantId,
            TenantName = tenantName,
            RestoredAt = ResolveRestoredAt(restore),
            RestoredBy = restore.ApprovedByUserId ?? restore.RequestedByUserId,
            BackupId = restore.BackupRunId,
            BackupDate = restore.BackupRun?.CompletedAt ?? restore.BackupRun?.RequestedAt,
            TablesRestored = restore.RestoreVerificationRun?.PgRestoreListLineCount,
            RecordsRestored = null,
            Status = MapStatus(restore.Status),
            ComplianceChecked = true,
            RksvCompliant = processOk && outcomeOk && fiscalOk,
            RksvComplianceNotes = RksvComplianceNotes,
            ComplianceFindings = findings,
            ValidationOnly = restore.ValidationOnly,
            TargetDatabaseName = restore.TargetDatabaseName,
            RestoreVerificationRunId = restore.RestoreVerificationRunId,
            DrillStatus = restore.RestoreVerificationRun?.Status.ToString(),
            FiscalSqlPassed = restore.RestoreVerificationRun?.FiscalSqlPassed,
            PostRestoreContinuityChecksPassed = restore.RestoreVerificationRun?.PostRestoreContinuityChecksPassed,
            CorrelationId = restore.CorrelationId
        };
    }

    private bool EvaluateProcessCompliance(ManualRestoreRequest restore, List<string> findings)
    {
        var ok = true;

        if (!restore.ValidationOnly)
        {
            ok = false;
            findings.Add("validation_only_required");
        }

        if (!IsIsolatedTarget(restore.TargetDatabaseName))
        {
            ok = false;
            findings.Add("target_not_isolated_validation_database");
        }
        else
        {
            findings.Add("isolated_target_ok");
        }

        findings.Add("no_fiscal_timestamp_rewrite");

        switch (restore.Status)
        {
            case ManualRestoreRequestStatus.PendingApproval:
                ok = false;
                findings.Add("dual_superadmin_approval_pending");
                break;
            case ManualRestoreRequestStatus.Rejected:
                findings.Add("dual_superadmin_rejected");
                break;
            default:
                if (string.IsNullOrWhiteSpace(restore.ApprovedByUserId))
                {
                    ok = false;
                    findings.Add("dual_superadmin_approval_missing");
                }
                else if (string.Equals(
                             restore.ApprovedByUserId,
                             restore.RequestedByUserId,
                             StringComparison.Ordinal))
                {
                    ok = false;
                    findings.Add("dual_superadmin_same_actor");
                }
                else
                {
                    findings.Add("dual_superadmin_approval_ok");
                }

                break;
        }

        return ok;
    }

    private static bool EvaluateOutcomeCompliance(ManualRestoreRequest restore, List<string> findings)
    {
        switch (restore.Status)
        {
            case ManualRestoreRequestStatus.PendingApproval:
            case ManualRestoreRequestStatus.Approved:
                findings.Add("restore_not_terminal");
                return true;
            case ManualRestoreRequestStatus.Rejected:
                findings.Add("restore_rejected_no_production_write");
                return true;
            case ManualRestoreRequestStatus.Failed:
                findings.Add("restore_failed");
                return false;
            case ManualRestoreRequestStatus.Completed:
                var drill = restore.RestoreVerificationRun;
                if (drill is null)
                {
                    findings.Add("completed_without_linked_drill");
                    return true;
                }

                if (drill.Status == RestoreVerificationStatus.Failed)
                {
                    findings.Add("linked_drill_failed");
                    return false;
                }

                if (drill.Status == RestoreVerificationStatus.Succeeded)
                {
                    findings.Add("linked_drill_succeeded");
                    return true;
                }

                findings.Add($"linked_drill_status_{drill.Status}");
                return false;
            default:
                findings.Add($"unknown_status_{restore.Status}");
                return false;
        }
    }

    private static bool EvaluateFiscalEvidence(RestoreVerificationRun? drill, List<string> findings)
    {
        if (drill is null)
            return true;

        var ok = true;

        if (!drill.FiscalSqlSkipped && drill.FiscalSqlPassed == false)
        {
            ok = false;
            findings.Add("fiscal_sql_failed");
        }
        else if (drill.FiscalSqlSkipped)
        {
            findings.Add(string.IsNullOrWhiteSpace(drill.FiscalSqlSkipReason)
                ? "fiscal_sql_skipped"
                : $"fiscal_sql_skipped:{drill.FiscalSqlSkipReason}");
        }
        else if (drill.FiscalSqlPassed == true)
        {
            findings.Add("fiscal_sql_passed");
        }

        if (drill.PostRestoreContinuityChecksExecuted && drill.PostRestoreContinuityChecksPassed == false)
        {
            ok = false;
            findings.Add("post_restore_continuity_failed");
        }
        else if (drill.PostRestoreContinuityChecksExecuted && drill.PostRestoreContinuityChecksPassed == true)
        {
            findings.Add("post_restore_continuity_passed");
        }

        return ok;
    }

    private bool IsIsolatedTarget(string targetDatabaseName)
    {
        try
        {
            _targetGuard.ValidateOrThrow(targetDatabaseName);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static DateTime? ResolveRestoredAt(ManualRestoreRequest restore) =>
        restore.Status switch
        {
            ManualRestoreRequestStatus.Completed or ManualRestoreRequestStatus.Failed
                => restore.RestoreVerificationRun?.CompletedAt ?? restore.ApprovedAt,
            ManualRestoreRequestStatus.Rejected => restore.ApprovedAt,
            ManualRestoreRequestStatus.Approved => restore.ApprovedAt,
            _ => restore.RequestedAt
        };

    private static string MapStatus(ManualRestoreRequestStatus status) => status switch
    {
        ManualRestoreRequestStatus.PendingApproval => "PendingApproval",
        ManualRestoreRequestStatus.Approved => "Approved",
        ManualRestoreRequestStatus.Rejected => "Rejected",
        ManualRestoreRequestStatus.Completed => "Completed",
        ManualRestoreRequestStatus.Failed => "Failed",
        _ => status.ToString()
    };
}
