namespace KasseAPI_Final.Models;

/// <summary>
/// Sprint 5: Audit retention policy. Minimum retention period; cleanup must not delete records newer than (today - RetentionYears).
/// </summary>
public class AuditRetentionOptions
{
    public const string SectionName = "AuditRetention";

    /// <summary>Minimum years to retain audit logs. Cleanup rejects cutoff dates that would delete records within this window. Default 7 (legal/fiscal retention).</summary>
    public int RetentionYears { get; set; } = 7;
}
