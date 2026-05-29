using KasseAPI_Final.Services;

namespace KasseAPI_Final.Models.DTOs;

public enum DemoImportJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}

public sealed record DemoImportJobStartResponseDto(string JobId, int TotalProducts);

public sealed record DemoImportCategoryProgressDto(
    string CategoryName,
    int Total,
    int Processed = 0,
    int Imported = 0,
    int Skipped = 0,
    /// <summary>Waiting | Processing | Completed</summary>
    string State = "Waiting");

public sealed record DemoImportProgressDto(
    string JobId = "",
    DemoImportJobStatus Status = DemoImportJobStatus.Queued,
    int TotalProducts = 0,
    int ProcessedProducts = 0,
    int ImportedCount = 0,
    int SkippedCount = 0,
    string? CurrentProductName = null,
    int Percent = 0,
    IReadOnlyList<DemoImportCategoryProgressDto>? Categories = null,
    ImportResult? Result = null,
    string? Message = null)
{
    public IReadOnlyList<DemoImportCategoryProgressDto> Categories { get; init; } =
        Categories ?? Array.Empty<DemoImportCategoryProgressDto>();
}

public sealed record DemoImportJobStatusDto(
    string JobId,
    DemoImportJobStatus Status,
    DemoImportProgressDto Progress);
