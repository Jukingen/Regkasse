namespace KasseAPI_Final.DTOs;

/// <summary>Unified Super Admin license audit projection (billing_audit_log + LICENSE_* audit_logs).</summary>
public sealed record LicenseAuditLogItemDto(
    Guid Id,
    DateTime CreatedAtUtc,
    Guid? TenantId,
    string? TenantName,
    string Action,
    string? FromStatus,
    string? ToStatus,
    string? PerformedBy,
    string? Reason);

public sealed record LicenseAuditLogListResponse(
    IReadOnlyList<LicenseAuditLogItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record LicenseAuditLogQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? TenantId = null,
    string? Action = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);
