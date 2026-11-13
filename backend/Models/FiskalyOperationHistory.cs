using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models;

/// <summary>Persisted status for Fiskaly FA receipt operations.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FiskalyOperationHistoryStatus
{
    Pending,
    Processing,
    Success,
    Failed,
}

/// <summary>Canonical status strings and UI helpers. DB stores <see cref="FiskalyOperationHistoryStatus.Success"/>; the FA label is “Completed”.</summary>
public static class FiskalyOperationHistoryStatuses
{
    public const string Pending = nameof(FiskalyOperationHistoryStatus.Pending);
    public const string Processing = nameof(FiskalyOperationHistoryStatus.Processing);
    public const string Success = nameof(FiskalyOperationHistoryStatus.Success);
    public const string Failed = nameof(FiskalyOperationHistoryStatus.Failed);

    public static bool IsInFlight(string? status) =>
        string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Processing, StringComparison.OrdinalIgnoreCase);

    public static bool IsTerminal(string? status) =>
        string.Equals(status, Success, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Failed, StringComparison.OrdinalIgnoreCase);

    public static bool CanRetry(string? status) =>
        string.Equals(status, Failed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase);

    public static int ProgressPercent(string? status)
    {
        if (string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase))
            return 5;
        if (string.Equals(status, Processing, StringComparison.OrdinalIgnoreCase))
            return 55;
        if (IsTerminal(status))
            return 100;
        return 0;
    }

    /// <summary>Maps UI alias <c>Completed</c> to stored <see cref="Success"/>.</summary>
    public static string? NormalizeFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;
        var trimmed = status.Trim();
        if (string.Equals(trimmed, "Completed", StringComparison.OrdinalIgnoreCase))
            return Success;
        return trimmed;
    }
}

/// <summary>Canonical operation kinds recorded on <see cref="FiskalyOperationHistory"/>.</summary>
public static class FiskalyOperationTypes
{
    public const string Normal = "normal";
    public const string Cancel = "cancel";
    public const string Nullbeleg = "nullbeleg";
    public const string Startbeleg = "startbeleg";
    public const string Monatsbeleg = "monatsbeleg";
    public const string Jahresbeleg = "jahresbeleg";
    public const string Schlussbeleg = "schlussbeleg";
    public const string Tagesabschluss = "tagesabschluss";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Normal, Cancel, Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg, Tagesabschluss
    };

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}

/// <summary>Operator triage state for failed Fiskaly history rows.</summary>
public static class FiskalyErrorReviewStatuses
{
    public const string Open = "open";
    public const string Resolved = "resolved";
    public const string KnownIssue = "known_issue";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Open, Resolved, KnownIssue
    };

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Open;
        var trimmed = value.Trim().ToLowerInvariant().Replace('-', '_');
        if (trimmed is "knownissue")
            return KnownIssue;
        return IsKnown(trimmed) ? trimmed : Open;
    }
}

/// <summary>Tenant-scoped audit row for each Fiskaly/RKSV operation executed from FA.</summary>
[Table("fiskaly_operation_history")]
public sealed class FiskalyOperationHistory : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [MaxLength(200)]
    [Column("tenant_name")]
    public string? TenantName { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("operation_type")]
    public string OperationType { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = nameof(FiskalyOperationHistoryStatus.Pending);

    [Required]
    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [MaxLength(160)]
    [Column("cash_register_name")]
    public string? CashRegisterName { get; set; }

    [MaxLength(64)]
    [Column("receipt_number")]
    public string? ReceiptNumber { get; set; }

    [MaxLength(80)]
    [Column("receipt_id")]
    public string? ReceiptId { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("user_display_name")]
    public string? UserDisplayName { get; set; }

    [Column("request_payload_json", TypeName = "jsonb")]
    public string? RequestPayloadJson { get; set; }

    [Column("response_payload_json", TypeName = "jsonb")]
    public string? ResponsePayloadJson { get; set; }

    [MaxLength(64)]
    [Column("error_code")]
    public string? ErrorCode { get; set; }

    [Column("error_message", TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [Column("retried_from_id")]
    public Guid? RetriedFromId { get; set; }

    [Required]
    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Required]
    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("completed_at_utc")]
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Operator triage: <c>open</c>, <c>resolved</c>, or <c>known_issue</c>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("error_review_status")]
    public string ErrorReviewStatus { get; set; } = FiskalyErrorReviewStatuses.Open;

    [Column("error_reviewed_at_utc")]
    public DateTime? ErrorReviewedAtUtc { get; set; }

    [MaxLength(450)]
    [Column("error_reviewed_by_user_id")]
    public string? ErrorReviewedByUserId { get; set; }
}
