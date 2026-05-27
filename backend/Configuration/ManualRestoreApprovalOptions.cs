namespace KasseAPI_Final.Configuration;

/// <summary>Manual restore approval workflow (Super Admin, validation-only isolated DB).</summary>
public sealed class ManualRestoreApprovalOptions
{
    public const string SectionName = "ManualRestoreApproval";

    /// <summary>When false, create/approve endpoints reject with 503.</summary>
    public bool Enabled { get; set; } = true;

    public int ApprovalTokenTtlMinutes { get; set; } = 15;

    /// <summary>Required prefix for <see cref="DTOs.RestoreRequest.TargetDatabaseName"/>.</summary>
    public string TargetDatabaseNamePrefix { get; set; } = "restore_validation_";

    /// <summary>Extra database names that must never be used as restore targets (case-insensitive).</summary>
    public string[] AdditionalBlockedDatabaseNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional static approver inboxes when no other SuperAdmin users with email exist.
    /// </summary>
    public string[] FallbackApproverEmails { get; set; } = Array.Empty<string>();
}
