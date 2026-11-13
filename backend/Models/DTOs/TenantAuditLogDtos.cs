namespace KasseAPI_Final.Models.DTOs;

/// <summary>Lightweight tenant-scoped audit row for Super Admin review.</summary>
public sealed class TenantAuditLogItemDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserDisplayName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>Paginated tenant audit log response.</summary>
public sealed class TenantAuditLogsResponse
{
    public bool Success { get; set; } = true;
    public Guid TenantId { get; set; }
    public List<TenantAuditLogItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string Message { get; set; } = string.Empty;
}
