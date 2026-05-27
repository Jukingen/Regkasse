namespace KasseAPI_Final.Models.DTOs;

public enum BulkImportJobStatus
{
    Queued,
    Running,
    Completed,
    Cancelled,
    Failed,
}

public class BulkImportErrorDto
{
    public int Row { get; set; }
    public string? Email { get; set; }
    public string Error { get; set; } = string.Empty;
}

/// <summary>Final import outcome (also embedded in job status when complete).</summary>
public class BulkImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<BulkImportErrorDto> Errors { get; set; } = Array.Empty<BulkImportErrorDto>();
    public string? DownloadUrl { get; set; }
}

public sealed class BulkImportJobStatusDto
{
    public string JobId { get; set; } = string.Empty;
    public BulkImportJobStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    /// <summary>Recent row errors (capped for polling payloads).</summary>
    public IReadOnlyList<BulkImportErrorDto> Errors { get; set; } = Array.Empty<BulkImportErrorDto>();
    public BulkImportResultDto? Result { get; set; }
    public string? Message { get; set; }
}

// Backward-compatible aliases for existing callers/tests.
public sealed class BulkUserImportResponseDto : BulkImportResultDto;

public sealed class BulkUserImportErrorDto : BulkImportErrorDto;

public sealed class BulkUserImportRow : BulkImportRow;
