using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class ApprovalRequestDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string? RequestedByEmail { get; set; }
    public string? RequestedByDisplayName { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ApprovedByEmail { get; set; }
    public string? ApprovedByDisplayName { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string? PathHint { get; set; }

    /// <summary>Minutes from request to decision (approved/rejected); null when still open.</summary>
    public int? TimeToDecisionMinutes { get; set; }
}

public sealed class ApprovalHistoryQuery
{
    public Guid? TenantId { get; set; }
    public string? Status { get; set; }
    public string? ActionType { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; }
}

public sealed class ApprovalHistoryReportDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int TotalRequests { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int ExpiredCount { get; set; }
    public int ConsumedCount { get; set; }
    public double? AverageTimeToApprovalMinutes { get; set; }
    public double? MedianTimeToApprovalMinutes { get; set; }
    public IReadOnlyList<ApprovalActionTypeCountDto> ByActionType { get; set; } =
        Array.Empty<ApprovalActionTypeCountDto>();
    public IReadOnlyList<ApprovalRequestDto> Recent { get; set; } = Array.Empty<ApprovalRequestDto>();
}

public sealed class ApprovalActionTypeCountDto
{
    public string ActionType { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
}

public sealed class CreateApprovalRequestDto
{
    public CriticalActionType ActionType { get; set; }
    public Guid? TenantId { get; set; }
    public string? PathHint { get; set; }
    public string? Payload { get; set; }
    public string? Reason { get; set; }
}

public sealed class ResolveApprovalRequestDto
{
    public string? Notes { get; set; }
}

public sealed class ApprovalMutationResultDto
{
    public bool Succeeded { get; set; }
    public Guid? RequestId { get; set; }
    public string? ApprovalToken { get; set; }
    public string HeaderName { get; set; } = CriticalActionOptions.ApprovalHeaderName;
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}
