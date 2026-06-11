using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class DepExportHistoryResponse
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public DateTime ExportedAt { get; set; }
    public string ExportedByUserId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int SignatureCount { get; set; }
    public int GroupCount { get; set; }
    public DepExportStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasStoredFile { get; set; }
    public Guid? ScheduleId { get; set; }
    public bool IncludeSpecialReceipts { get; set; }
    public bool IncludeDailyClosings { get; set; }
}

public sealed class DepExportScheduleResponse
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public string ScheduleType { get; set; } = DepExportScheduleTypes.Monthly;
    public int DayOfMonth { get; set; }
    public string TimeOfDay { get; set; } = "00:00";
    public bool IsActive { get; set; }
    public string? RecipientEmails { get; set; }
    public DateTime LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateDepExportScheduleRequest
{
    public Guid CashRegisterId { get; set; }

    public string ScheduleType { get; set; } = DepExportScheduleTypes.Monthly;

    public int DayOfMonth { get; set; } = 1;

    public string TimeOfDay { get; set; } = "02:00";

    public string? RecipientEmails { get; set; }
}

public sealed class DepExportHistoryListResponse
{
    public IReadOnlyList<DepExportHistoryResponse> Items { get; set; } = Array.Empty<DepExportHistoryResponse>();
    public int TotalCount { get; set; }
}
