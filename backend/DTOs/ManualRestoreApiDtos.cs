using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

/// <summary>Create manual restore (validation-only); requires second Super Admin approval.</summary>
public sealed class RestoreRequest
{
    [Required]
    [JsonPropertyName("backupRunId")]
    public Guid BackupRunId { get; set; }

    [Required]
    [MaxLength(63)]
    [JsonPropertyName("targetDatabaseName")]
    public string TargetDatabaseName { get; set; } = string.Empty;

    /// <summary>Must be true; production restore is not supported via this API.</summary>
    [Required]
    [JsonPropertyName("validationOnly")]
    public bool ValidationOnly { get; set; } = true;

    [MaxLength(2000)]
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public sealed class RestoreApprovalRequest
{
    [Required]
    [JsonPropertyName("approvalToken")]
    public string ApprovalToken { get; set; } = string.Empty;

    /// <summary><c>approve</c> or <c>reject</c>.</summary>
    [Required]
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [MaxLength(2000)]
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public sealed class RestoreRequestStatus
{
    [JsonPropertyName("requestId")]
    public Guid RequestId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("requestedAt")]
    public DateTime RequestedAt { get; set; }

    [JsonPropertyName("requestedByUserId")]
    public string? RequestedByUserId { get; set; }

    [JsonPropertyName("requestedByEmail")]
    public string? RequestedByEmail { get; set; }

    /// <summary>Never returned by API; token is delivered out-of-band (email).</summary>
    [JsonPropertyName("approvalToken")]
    public string? ApprovalToken { get; set; }

    [JsonPropertyName("approvedByUserId")]
    public string? ApprovedByUserId { get; set; }

    [JsonPropertyName("approvedAt")]
    public DateTime? ApprovedAt { get; set; }

    [JsonPropertyName("rejectionReason")]
    public string? RejectionReason { get; set; }

    /// <summary>Requester's reason from create step (not the reject reason).</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("backupRunId")]
    public Guid BackupRunId { get; set; }

    [JsonPropertyName("targetDatabaseName")]
    public string TargetDatabaseName { get; set; } = string.Empty;

    [JsonPropertyName("validationOnly")]
    public bool ValidationOnly { get; set; }

    [JsonPropertyName("restoreVerificationRunId")]
    public Guid? RestoreVerificationRunId { get; set; }
}

public sealed class RestoreRequestHistoryResponse
{
    [JsonPropertyName("items")]
    public List<RestoreRequestStatus> Items { get; set; } = new();

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

/// <summary>
/// RKSV-oriented restore compliance report for a manual validation restore request.
/// </summary>
public sealed class RestoreReportResponseDto
{
    [JsonPropertyName("restoreId")]
    public Guid RestoreId { get; set; }

    [JsonPropertyName("tenantId")]
    public Guid? TenantId { get; set; }

    [JsonPropertyName("tenantName")]
    public string? TenantName { get; set; }

    [JsonPropertyName("restoredAt")]
    public DateTime? RestoredAt { get; set; }

    [JsonPropertyName("restoredBy")]
    public string? RestoredBy { get; set; }

    [JsonPropertyName("backupId")]
    public Guid BackupId { get; set; }

    [JsonPropertyName("backupDate")]
    public DateTime? BackupDate { get; set; }

    /// <summary>Best-effort from drill TOC line count when present; not a row inventory.</summary>
    [JsonPropertyName("tablesRestored")]
    public int? TablesRestored { get; set; }

    [JsonPropertyName("recordsRestored")]
    public long? RecordsRestored { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("complianceChecked")]
    public bool ComplianceChecked { get; set; }

    [JsonPropertyName("rksvCompliant")]
    public bool RksvCompliant { get; set; }

    [JsonPropertyName("rksvComplianceNotes")]
    public string RksvComplianceNotes { get; set; } = string.Empty;

    [JsonPropertyName("complianceFindings")]
    public List<string> ComplianceFindings { get; set; } = new();

    [JsonPropertyName("validationOnly")]
    public bool ValidationOnly { get; set; }

    [JsonPropertyName("targetDatabaseName")]
    public string TargetDatabaseName { get; set; } = string.Empty;

    [JsonPropertyName("restoreVerificationRunId")]
    public Guid? RestoreVerificationRunId { get; set; }

    [JsonPropertyName("drillStatus")]
    public string? DrillStatus { get; set; }

    [JsonPropertyName("fiscalSqlPassed")]
    public bool? FiscalSqlPassed { get; set; }

    [JsonPropertyName("postRestoreContinuityChecksPassed")]
    public bool? PostRestoreContinuityChecksPassed { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}

/// <summary>Pre-restore RKSV compliance check response.</summary>
public sealed class RestoreComplianceCheckResponseDto
{
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("backupRunId")]
    public Guid? BackupRunId { get; set; }

    [JsonPropertyName("tenantId")]
    public Guid? TenantId { get; set; }

    [JsonPropertyName("checks")]
    public List<RestoreComplianceCheckItemDto> Checks { get; set; } = new();
}

public sealed class RestoreComplianceCheckItemDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}
