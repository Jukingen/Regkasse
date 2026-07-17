namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Operator-facing restore compliance report for a manual validation restore request.
/// Source of truth is <c>manual_restore_requests</c> (+ linked drill), not a fictional restore history table.
/// </summary>
public sealed class RestoreReport
{
    public Guid RestoreId { get; init; }

    public Guid? TenantId { get; init; }

    public string? TenantName { get; init; }

    /// <summary>Completion / failure time when terminal; otherwise approval or request time.</summary>
    public DateTime? RestoredAt { get; init; }

    public string? RestoredBy { get; init; }

    public Guid BackupId { get; init; }

    public DateTime? BackupDate { get; init; }

    /// <summary>
    /// Best-effort table/object count from drill TOC line count when available; otherwise null.
    /// Not a guaranteed restored-row inventory.
    /// </summary>
    public int? TablesRestored { get; init; }

    /// <summary>Row counts are not tracked by the validation restore pipeline; always null today.</summary>
    public long? RecordsRestored { get; init; }

    public string Status { get; init; } = string.Empty;

    /// <summary>True when this report evaluated product RKSV restore controls.</summary>
    public bool ComplianceChecked { get; init; }

    /// <summary>
    /// True when process controls passed and (for terminal success) the linked drill did not fail RKSV gates.
    /// Never hard-coded; see <see cref="ComplianceFindings"/>.
    /// </summary>
    public bool RksvCompliant { get; init; }

    /// <summary>Canonical notes stamped on restore audit events.</summary>
    public string RksvComplianceNotes { get; init; } =
        "validation_only;isolated_target;no_production_write;no_fiscal_timestamp_rewrite;dual_superadmin_approval";

    public IReadOnlyList<string> ComplianceFindings { get; init; } = Array.Empty<string>();

    public bool ValidationOnly { get; init; }

    public string TargetDatabaseName { get; init; } = string.Empty;

    public Guid? RestoreVerificationRunId { get; init; }

    public string? DrillStatus { get; init; }

    public bool? FiscalSqlPassed { get; init; }

    public bool? PostRestoreContinuityChecksPassed { get; init; }

    public string? CorrelationId { get; init; }
}
