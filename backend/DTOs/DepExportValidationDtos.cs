namespace KasseAPI_Final.DTOs;

/// <summary>One automatic DEP export validation check.</summary>
public sealed class DepExportValidationCheck
{
    public string Name { get; set; } = string.Empty;

    public bool Passed { get; set; }

    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Result of validating a stored DEP export history row (BMF JSON structure + signatures + certs + tax payloads).
/// Distinct from structural-only <c>RksvDepExportValidationResult</c>.
/// </summary>
public sealed class DepExportHistoryValidationResult
{
    public Guid ExportId { get; set; }

    public Guid TenantId { get; set; }

    public bool IsValid { get; set; }

    public IReadOnlyList<DepExportValidationCheck> Checks { get; set; } = Array.Empty<DepExportValidationCheck>();

    public DateTime ValidatedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public static DepExportHistoryValidationResult Fail(Guid exportId, string message) =>
        new()
        {
            ExportId = exportId,
            IsValid = false,
            ErrorMessage = message,
            ValidatedAt = DateTime.UtcNow,
            Checks = Array.Empty<DepExportValidationCheck>(),
        };
}

/// <summary>Tenant-scoped aggregate of recent DEP export validation outcomes.</summary>
public sealed class DepExportValidationReport
{
    public Guid TenantId { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public int TotalExports { get; set; }

    public int PassedCount { get; set; }

    public int FailedCount { get; set; }

    public int PendingCount { get; set; }

    public int SkippedCount { get; set; }

    public bool AllValidatedPassed { get; set; }

    public IReadOnlyList<DepExportHistoryValidationSummaryItem> Recent { get; set; } =
        Array.Empty<DepExportHistoryValidationSummaryItem>();
}

public sealed class DepExportHistoryValidationSummaryItem
{
    public Guid ExportId { get; set; }

    public Guid CashRegisterId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime ExportedAt { get; set; }

    public string? ValidationStatus { get; set; }

    public DateTime? ValidatedAt { get; set; }

    public bool? IsValid { get; set; }
}
