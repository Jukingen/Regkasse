using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public static class FiskalyReceiptErrorCodes
{
    public const string FiskalyDisabled = "FISKALY_DISABLED";
    public const string FiskalyNotConfigured = "FISKALY_NOT_CONFIGURED";
    public const string FiskalyLiveBlocked = "FISKALY_LIVE_BLOCKED";
    public const string FiskalyAuthFailed = "FISKALY_AUTH_FAILED";
    public const string FiskalyRegisterNotInitialized = "FISKALY_REGISTER_NOT_INITIALIZED";
    public const string FiskalyRegisterNotFound = "FISKALY_REGISTER_NOT_FOUND";
    public const string FiskalyApiError = "FISKALY_API_ERROR";
    public const string TseUnavailable = "TSE_UNAVAILABLE";
    public const string CashRegisterNotFound = "CASH_REGISTER_NOT_FOUND";
    public const string CashRegisterIdRequired = "CASH_REGISTER_ID_REQUIRED";
    public const string CashRegisterMismatch = "CASH_REGISTER_MISMATCH";
    public const string PaymentNotFound = "PAYMENT_NOT_FOUND";
    public const string StornoFailed = "STORNO_FAILED";
    public const string ApprovalRequired = "REVERSAL_APPROVAL_REQUIRED";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string SpecialReceiptFailed = "SPECIAL_RECEIPT_FAILED";
    public const string ClosingNotFound = "CLOSING_NOT_FOUND";
    public const string ClosingNotDaily = "CLOSING_NOT_DAILY";
    public const string TseSignatureRequired = "TSE_SIGNATURE_REQUIRED";
    public const string FutureClosingDate = "FUTURE_CLOSING_DATE";
    public const string StartbelegRequired = "STARTBELEG_REQUIRED";
    public const string InvalidPeriod = "INVALID_PERIOD";
    public const string PeriodNotCompleted = "PERIOD_NOT_COMPLETED";
}

public sealed class FiskalyReceiptErrorDto
{
    public string Code { get; init; } = FiskalyReceiptErrorCodes.FiskalyApiError;

    public string Message { get; init; } = string.Empty;

    public string? Details { get; init; }
}

public sealed class FiskalyReceiptDataDto
{
    public string ReceiptId { get; init; } = string.Empty;

    public string ReceiptNumber { get; init; } = string.Empty;

    public string? Signature { get; init; }

    public string? QrCode { get; init; }
}

public sealed class FiskalyReceiptEnvelopeDto
{
    public bool Success { get; init; }

    public FiskalyReceiptDataDto? Data { get; init; }

    public FiskalyReceiptErrorDto? Error { get; init; }

    /// <summary>History row created for this attempt, when recording succeeded.</summary>
    public Guid? HistoryId { get; init; }
}

public sealed class FiskalyNormalReceiptRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [Range(typeof(decimal), "0", "1000000")]
    public decimal? Amount { get; set; }

    [MaxLength(32)]
    public string? VatRate { get; set; }
}

public sealed class FiskalyCancelReceiptRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    /// <summary>Local payment id of the original fiscal receipt (not the fiskaly remote id).</summary>
    [Required]
    public Guid OriginalReceiptId { get; set; }

    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class FiskalyCashRegisterReceiptRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [MaxLength(450)]
    public string? Reason { get; set; }
}

public sealed class FiskalyMonatsbelegReceiptRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [MaxLength(450)]
    public string? Reason { get; set; }
}

public sealed class FiskalyJahresbelegReceiptRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [MaxLength(450)]
    public string? Reason { get; set; }
}

public sealed class FiskalyTagesabschlussReceiptRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    /// <summary>Vienna business day. Null = today. Must already have a TSE-signed Daily closing.</summary>
    public DateTime? ClosingDate { get; set; }
}

public sealed class FiskalyNullbelegReceiptRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [Range(2000, 2100)]
    public int? Year { get; set; }

    [Range(1, 12)]
    public int? Month { get; set; }

    [MaxLength(450)]
    public string? Reason { get; set; }
}
