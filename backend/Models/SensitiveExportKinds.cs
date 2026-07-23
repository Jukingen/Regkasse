namespace KasseAPI_Final.Models;

/// <summary>Sensitive export categories that may require approval / 2FA / privacy ack.</summary>
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

    public static bool RequiresCriticalTwoFactor(string kind) =>
        string.Equals(kind, SystemBackup, StringComparison.OrdinalIgnoreCase)
        || string.Equals(kind, AuditLogExport, StringComparison.OrdinalIgnoreCase);

    public static bool RequiresPrivacyAck(string kind) => IsValid(kind);
}
