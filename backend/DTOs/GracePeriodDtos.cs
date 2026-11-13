using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

public sealed class GracePeriodRuleDto
{
    public string ActionKind { get; set; } = string.Empty;
    public string Duration { get; set; } = "00:00:00";
    public double DurationSeconds { get; set; }
    public bool RequiresApproval { get; set; }
}

public sealed class GracePeriodsConfigDto
{
    public bool Enabled { get; set; }
    public IReadOnlyList<GracePeriodRuleDto> Rules { get; set; } = Array.Empty<GracePeriodRuleDto>();
}

public sealed class GracePeriodPendingDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ActionKind { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool CanCancel { get; set; }
    public double RemainingSeconds { get; set; }
    public string? CreatedBy { get; set; }
    public Guid? OperationLogId { get; set; }
}

public sealed class ScheduleGracePeriodRequest
{
    [Required]
    [MaxLength(64)]
    public string ActionKind { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string EntityId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>Optional JSON payload (e.g. decommission reason).</summary>
    public string? Payload { get; set; }
}

public sealed class ScheduleGracePeriodResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GracePeriodPendingDto? Pending { get; set; }
}

public sealed class CancelGracePeriodRequest
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}
