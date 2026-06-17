namespace KasseAPI_Final.Models.DTOs;

/// <summary>Audit log list API envelope (keyset or offset pagination).</summary>
public sealed class AuditLogsResponse
{
    public bool Success { get; set; }
    public List<AuditLogEntryDto> AuditLogs { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
    public string Message { get; set; } = string.Empty;
}
