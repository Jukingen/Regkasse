using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public static class FiskalyBatchErrorCodes
{
    public const string BatchTooLarge = "BATCH_TOO_LARGE";
    public const string BatchEmpty = "BATCH_EMPTY";
    public const string BatchValidation = "BATCH_VALIDATION";
    public const string BatchForbiddenKind = "BATCH_FORBIDDEN_KIND";
}

public sealed class FiskalyBatchLimitsDto
{
    public int MaxItems { get; init; }

    public int WarnAtItems { get; init; }
}

public sealed class FiskalyBatchErrorDto
{
    public bool Success { get; init; }

    public string Code { get; init; } = FiskalyBatchErrorCodes.BatchValidation;

    public string Message { get; init; } = string.Empty;

    public int? MaxItems { get; init; }
}

public sealed class FiskalyBatchItemResultDto
{
    public string Key { get; init; } = string.Empty;

    public bool Success { get; init; }

    public Guid? HistoryId { get; init; }

    public string? Label { get; init; }

    public FiskalyReceiptErrorDto? Error { get; init; }
}

public sealed class FiskalyBatchOperationResultDto
{
    public Guid BatchId { get; init; }

    public int Total { get; init; }

    public int SuccessCount { get; init; }

    public int FailedCount { get; init; }

    public IReadOnlyList<FiskalyBatchItemResultDto> Results { get; init; } = [];
}

public sealed class FiskalyBatchStornoItemRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    /// <summary>Local payment id of the original fiscal receipt.</summary>
    [Required]
    public Guid OriginalReceiptId { get; set; }

    [MaxLength(80)]
    public string? ReceiptNumber { get; set; }
}

public sealed class FiskalyBatchStornoRequest
{
    public Guid? BatchId { get; set; }

    [Required]
    [MinLength(1)]
    public List<FiskalyBatchStornoItemRequest> Items { get; set; } = [];

    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class FiskalyBatchSonderbelegeRequest
{
    public Guid? BatchId { get; set; }

    [Required]
    [MaxLength(32)]
    public string Kind { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<Guid> CashRegisterIds { get; set; } = [];

    [Range(2000, 2100)]
    public int? Year { get; set; }

    [Range(1, 12)]
    public int? Month { get; set; }

    [MaxLength(450)]
    public string? Reason { get; set; }
}

public sealed class FiskalyBatchDepExportRequest
{
    public Guid? BatchId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> TenantIds { get; set; } = [];

    [Required]
    public DateTime FromUtc { get; set; }

    [Required]
    public DateTime ToUtc { get; set; }

    public bool IncludeSpecialReceipts { get; set; } = true;

    public bool IncludeDailyClosings { get; set; } = true;
}

public sealed class FiskalyBatchProgressEventDto
{
    public Guid BatchId { get; init; }

    public Guid? TenantId { get; init; }

    public string Kind { get; init; } = string.Empty;

    public int Current { get; init; }

    public int Total { get; init; }

    public string? CurrentLabel { get; init; }

    public int SuccessCount { get; init; }

    public int FailedCount { get; init; }

    public bool Done { get; init; }
}

public sealed class FiskalyBatchDepExportResult
{
    public required byte[] ZipBytes { get; init; }

    public required string FileName { get; init; }

    public required FiskalyBatchOperationResultDto Summary { get; init; }
}
