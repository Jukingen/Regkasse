namespace KasseAPI_Final.Models;

/// <summary>Sensitive export categories that may require approval / privacy ack.</summary>
public static class SensitiveExportKinds
{
    public const string GdprDataExport = "gdpr-data-export";
    public const string SystemBackup = "system-backup";
    public const string AuditLogExport = "audit-log-export";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        GdprDataExport,
        SystemBackup,
        AuditLogExport,
    };

    public static bool IsValid(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && All.Contains(kind.Trim());

    /// <summary>
    /// Legacy classifier for kinds that formerly required step-up TOTP.
    /// Download security no longer enforces 2FA; kept for policy DTO compatibility.
    /// </summary>
    public static bool RequiresCriticalTwoFactor(string kind)
    {
        _ = kind;
        return false;
    }

    public static bool RequiresPrivacyAck(string kind) => IsValid(kind);
}
