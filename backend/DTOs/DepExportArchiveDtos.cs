namespace KasseAPI_Final.DTOs;

/// <summary>Result of archiving one DEP export history row.</summary>
public sealed class DepExportArchiveResult
{
    public Guid ExportId { get; set; }

    public bool Success { get; set; }

    public string? ArchivePath { get; set; }

    public string? Checksum { get; set; }

    public DateTime? RetentionUntil { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public bool AlreadyArchived { get; set; }

    public static DepExportArchiveResult Fail(Guid exportId, string message) =>
        new()
        {
            ExportId = exportId,
            Success = false,
            ErrorMessage = message,
        };

    public static DepExportArchiveResult Ok(
        Guid exportId,
        string archivePath,
        string checksum,
        DateTime archivedAt,
        DateTime retentionUntil,
        bool alreadyArchived = false) =>
        new()
        {
            ExportId = exportId,
            Success = true,
            ArchivePath = archivePath,
            Checksum = checksum,
            ArchivedAt = archivedAt,
            RetentionUntil = retentionUntil,
            AlreadyArchived = alreadyArchived,
        };
}

/// <summary>Tenant aggregate of DEP export archive state.</summary>
public sealed class DepExportArchiveReport
{
    public Guid TenantId { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public int TotalCompletedExports { get; set; }

    public int ArchivedCount { get; set; }

    public int PendingArchiveCount { get; set; }

    public int PurgedCount { get; set; }

    public int RetentionYears { get; set; }

    /// <summary>Sum of <see cref="DepExportArchiveSummaryItem.FileSizeBytes"/> for active (non-purged) archives.</summary>
    public long TotalArchivedSizeBytes { get; set; }

    /// <summary>Oldest <c>ExportedAt</c> among active archived exports.</summary>
    public DateTime? OldestArchivedExportAt { get; set; }

    public IReadOnlyList<DepExportArchiveSummaryItem> Recent { get; set; } =
        Array.Empty<DepExportArchiveSummaryItem>();
}

public sealed class DepExportArchiveSummaryItem
{
    public Guid ExportId { get; set; }

    public Guid CashRegisterId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime ExportedAt { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public DateTime? RetentionUntil { get; set; }

    public DateTime? PurgedAt { get; set; }

    public string? ArchiveChecksum { get; set; }

    public bool HasArchiveFile { get; set; }
}

/// <summary>Outcome of a purge sweep.</summary>
public sealed class DepExportPurgeResult
{
    public int ExaminedCount { get; set; }

    public int PurgedCount { get; set; }

    public int FailedCount { get; set; }

    public DateTime CutoffUtc { get; set; }
}
