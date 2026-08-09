namespace KasseAPI_Final.Services;

/// <summary>Outcome of a DEP export download open attempt (for audit + HTTP mapping).</summary>
public enum DepExportDownloadFailureKind
{
    NotFound = 0,
    TokenExpired = 1,
    FileMissing = 2,
    Purged = 3,
    ForbiddenTenant = 4,
    /// <summary>Hot retention window elapsed and no archive/hot file remains.</summary>
    HotExpired = 5,
}

/// <summary>Successful open of a stored DEP export JSON file.</summary>
public sealed class DepExportDownloadOpen
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required Guid ExportId { get; init; }
    public required Guid TenantId { get; init; }
}

/// <summary>Result of opening a DEP export for download (success or typed failure).</summary>
public sealed class DepExportDownloadAttempt
{
    public DepExportDownloadOpen? Open { get; init; }
    public DepExportDownloadFailureKind? Failure { get; init; }
    public Guid? ExportId { get; init; }
    public string? FileName { get; init; }

    public static DepExportDownloadAttempt Success(DepExportDownloadOpen open) =>
        new()
        {
            Open = open,
            ExportId = open.ExportId,
            FileName = open.FileName,
        };

    public static DepExportDownloadAttempt Fail(
        DepExportDownloadFailureKind failure,
        Guid? exportId = null,
        string? fileName = null) =>
        new()
        {
            Failure = failure,
            ExportId = exportId,
            FileName = fileName,
        };
}

/// <summary>Result of a hot-storage / token / stale-metadata cleanup sweep.</summary>
public sealed class DepExportStorageCleanupResult
{
    public int HotFilesDeleted { get; set; }
    public int TokensCleared { get; set; }
    public int MetadataRowsDeleted { get; set; }
    public int FailedCount { get; set; }
    public DateTime CutoffUtc { get; set; }
}
