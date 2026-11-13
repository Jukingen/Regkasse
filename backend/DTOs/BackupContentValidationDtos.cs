namespace KasseAPI_Final.DTOs;

/// <summary>Content validation of a backup package (manifest counts vs live DB + fiscal checks).</summary>
public sealed class BackupContentValidationDto
{
    public Guid RunId { get; init; }

    public DateTime ValidatedAtUtc { get; init; }

    /// <summary>Persisted <c>backup_verifications.id</c> when written.</summary>
    public Guid? VerificationId { get; init; }

    /// <summary>Alias of <see cref="OverallStatus"/> (Passed | Failed | Partial | Unavailable).</summary>
    public string Status => OverallStatus;

    /// <summary>Passed | Failed | Partial | Unavailable</summary>
    public string OverallStatus { get; init; } = BackupContentValidationStatuses.Unavailable;

    public string? Summary { get; init; }

    public string Strategy { get; init; } = string.Empty;

    public IReadOnlyList<BackupContentTableValidationDto> Tables { get; init; } =
        Array.Empty<BackupContentTableValidationDto>();

    /// <summary>Named fiscal integrity checks (receipt sequence, signature chain, presence).</summary>
    public IReadOnlyList<BackupContentFiscalCheckDto> FiscalChecks { get; init; } =
        Array.Empty<BackupContentFiscalCheckDto>();

    /// <summary>Aggregate fiscal summary (compat with earlier FA clients).</summary>
    public BackupContentFiscalValidationDto? Fiscal { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public static class BackupContentValidationStatuses
{
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Partial = "Partial";
    public const string Unavailable = "Unavailable";
}

public sealed class BackupContentTableValidationDto
{
    /// <summary>Manifest / package key (e.g. products.json).</summary>
    public string TableKey { get; init; } = string.Empty;

    /// <summary>Alias of <see cref="TableKey"/> for report consumers.</summary>
    public string TableName => TableKey;

    public int? ManifestCount { get; init; }

    public int? LiveCount { get; init; }

    /// <summary>Alias of <see cref="LiveCount"/>.</summary>
    public int? ActualCount => LiveCount;

    /// <summary>True when both counts are present and equal.</summary>
    public bool Match { get; init; }

    /// <summary>passed | warning | failed | skipped</summary>
    public string Status { get; init; } = "skipped";

    public string? Detail { get; init; }
}

public sealed class BackupContentFiscalCheckDto
{
    public string CheckName { get; init; } = string.Empty;

    public bool Passed { get; init; }

    public string? Details { get; init; }
}

public sealed class BackupContentFiscalValidationDto
{
    /// <summary>passed | warning | failed | skipped</summary>
    public string Status { get; init; } = "skipped";

    public int? PaymentsInManifest { get; init; }

    public int? ReceiptsInManifest { get; init; }

    public int? LiveSignedPayments { get; init; }

    public int? LiveUnsignedPayments { get; init; }

    public int? ChainBreakCount { get; init; }

    public int? SequenceGapCount { get; init; }

    public int? DuplicateReceiptCount { get; init; }

    public string? Detail { get; init; }
}

/// <summary>Alias request for POST /api/admin/backup/drill/run.</summary>
public sealed class RunRestoreDrillRequestDto
{
    /// <summary>Optional succeeded backup run to pin; null = orchestrator uses latest eligible dump.</summary>
    public Guid? BackupRunId { get; init; }

    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// Alias response for restore-verification trigger.
/// Drill work is async (hosted orchestrator); <see cref="Success"/> / <see cref="Status"/> reflect enqueue outcome unless the run already completed.
/// </summary>
public sealed class RestoreDrillResultDto
{
    public Guid RunId { get; init; }

    /// <summary>True when enqueue accepted (new or existing active/idempotent run returned without HTTP error).</summary>
    public bool Success { get; init; }

    /// <summary>Restore verification run status (Queued/Running/Succeeded/Failed/…).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Run completedAt when terminal; otherwise requestedAt (enqueue time).</summary>
    public DateTime CompletedAt { get; init; }

    public List<string> Errors { get; init; } = new();

    public Guid? SourceBackupRunId { get; init; }

    public bool NewQueuedRunCreated { get; init; }

    public bool ExistingRunReturned { get; init; }

    public string OrchestrationState { get; init; } = string.Empty;

    public RestoreVerificationRunResponseDto? Run { get; init; }

    public string AliasOf { get; init; } = "/api/admin/restore-verification/trigger";
}
