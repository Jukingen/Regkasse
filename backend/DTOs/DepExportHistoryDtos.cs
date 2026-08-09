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
    /// <summary>Pre-F5 JWS count; Prüftool-compatible when 0.</summary>
    public int LegacyJwsCount { get; set; }
    /// <summary>True when <see cref="LegacyJwsCount"/> is 0.</summary>
    public bool PrueftoolCompatible { get; set; } = true;
    public DepExportStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasStoredFile { get; set; }
    /// <summary>True when a non-expired download token is currently issued (token value is not returned).</summary>
    public bool HasActiveDownloadToken { get; set; }
    public DateTime? DownloadTokenExpiresAtUtc { get; set; }
    /// <summary>Relative download path when <see cref="HasStoredFile"/> is true.</summary>
    public string? DownloadUrl { get; set; }
    /// <summary>When the hot download copy expires (typically ExportedAt + 7 days).</summary>
    public DateTime? ExpiresAt { get; set; }
    /// <summary>Last successful download timestamp.</summary>
    public DateTime? DownloadedAt { get; set; }
    /// <summary>Successful download count.</summary>
    public int DownloadCount { get; set; }
    /// <summary>True when the caller may soft-delete this recent-export entry.</summary>
    public bool CanDelete { get; set; }
    public Guid? ScheduleId { get; set; }
    public bool IncludeSpecialReceipts { get; set; }
    public bool IncludeDailyClosings { get; set; }
    /// <summary>True when the export was created in RKSV/TSE simulation mode.</summary>
    public bool IsSimulated { get; set; }
    /// <summary>Operator note when <see cref="IsSimulated"/>.</summary>
    public string? SimulationNote { get; set; }
    public string? ValidationStatus { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? RetentionUntil { get; set; }
    public DateTime? PurgedAt { get; set; }
    public string? ArchiveChecksum { get; set; }
    public bool HasArchiveFile { get; set; }
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

/// <summary>Response for issuing / rotating a short-lived DEP download token.</summary>
public sealed class DepExportDownloadTokenResponse
{
    public Guid ExportId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    /// <summary>Relative API path: <c>/api/admin/rksv/dep-export/download/token/{token}</c>.</summary>
    public string DownloadPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

/// <summary>Most recent completed DEP export for the ambient tenant (compliance UX).</summary>
public sealed class DepExportLastExportResponse
{
    public bool HasExport { get; set; }
    public DateTime? LastExportAt { get; set; }
    /// <summary>Austria-local display string (dd.MM.yyyy HH:mm), UTC source stamped as UTC.</summary>
    public string? Formatted { get; set; }
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public bool IsSimulated { get; set; }
    public int DownloadCount { get; set; }
    public Guid? ExportId { get; set; }
    public Guid? CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
}

/// <summary>Current RKSV/TSE simulation flag plus optional last-export summary.</summary>
public sealed class DepExportStatusResponse
{
    public bool IsSimulated { get; set; }
    public string Environment { get; set; } = "Production";
    public string? SimulationNote { get; set; }
    public bool HasExport { get; set; }
    public DateTime? LastExportAt { get; set; }
    public bool? LastExportWasSimulated { get; set; }
}
