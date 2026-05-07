using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// Standard payment API envelope (OpenAPI-first, Orval-friendly). Opt-in via <c>X-Regkasse-Payment-Contract: v2</c>.
/// </summary>
public sealed class PaymentApiEnvelope<T>
{
    public const string CurrentApiVersion = "payment.v2";

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = CurrentApiVersion;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>Human-readable outcome; UI may localize via <see cref="Code"/>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("idempotency")]
    public PaymentIdempotencyInfo? Idempotency { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Idempotency semantics for payment operations.</summary>
public sealed class PaymentIdempotencyInfo
{
    /// <summary>True when this response replays a prior committed payment for the same idempotency key.</summary>
    [JsonPropertyName("replay")]
    public bool Replay { get; set; }

    /// <summary>Normalized key echoed when present on the request (optional).</summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }
}

/// <summary>Payload for POST payment create success (v2).</summary>
public sealed class PaymentCreateSuccessData
{
    [JsonPropertyName("paymentId")]
    public Guid PaymentId { get; set; }

    [JsonPropertyName("payment")]
    public object? Payment { get; set; }

    [JsonPropertyName("invoicePersisted")]
    public bool InvoicePersisted { get; set; }

    [JsonPropertyName("tse")]
    public PaymentCreateTseData? Tse { get; set; }

    /// <summary>Set when server-side NTP guard flagged clock drift (offline replay tolerance).</summary>
    [JsonPropertyName("timeSyncWarning")]
    public bool TimeSyncWarning { get; set; }

    /// <summary>True when payment was stored as server-side non-fiscal intent (TSE offline queue).</summary>
    [JsonPropertyName("nonFiscalOfflineQueued")]
    public bool NonFiscalOfflineQueued { get; set; }

    /// <summary>Offline intent row id when NonFiscalOfflineQueued is true.</summary>
    [JsonPropertyName("offlineTransactionId")]
    public Guid? OfflineTransactionId { get; set; }
}

public sealed class PaymentCreateTseData
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("isDemoFiscal")]
    public bool IsDemoFiscal { get; set; }

    [JsonPropertyName("qrPayload")]
    public string? QrPayload { get; set; }

    [JsonPropertyName("receiptNumber")]
    public string? ReceiptNumber { get; set; }
}

/// <summary>Unified payment error body (v2). Aligns with <see cref="ApiError"/> where possible.</summary>
public sealed class PaymentApiErrorBody
{
    public const string CurrentApiVersion = "payment.v2";

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = CurrentApiVersion;

    [JsonPropertyName("success")]
    public bool Success { get; set; } = false;

    /// <summary>Stable machine code, e.g. PAYMENT_TSE_DEVICE_NOT_READY.</summary>
    [Required]
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("fieldErrors")]
    public Dictionary<string, string[]>? FieldErrors { get; set; }

    /// <summary>Non-sensitive diagnostics: legacy diagnosticCode, HTTP hints, etc.</summary>
    [JsonPropertyName("context")]
    public PaymentApiErrorContext? Context { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PaymentApiErrorContext
{
    [JsonPropertyName("diagnosticCode")]
    public string? DiagnosticCode { get; set; }

    [JsonPropertyName("legacyErrors")]
    public List<string>? LegacyErrors { get; set; }
}

/// <summary>Stable payment API error codes (extend incrementally; do not rename).</summary>
public static class PaymentApiErrorCodes
{
    public const string Validation = "PAYMENT_VALIDATION_FAILED";
    public const string Unauthorized = "PAYMENT_UNAUTHORIZED";
    public const string ForbiddenRegister = "PAYMENT_REGISTER_FORBIDDEN";
    public const string RegisterPolicy = "PAYMENT_REGISTER_POLICY";
    public const string TseNotConnected = "PAYMENT_TSE_NOT_CONNECTED";
    public const string TseNotReady = "PAYMENT_TSE_NOT_READY";
    public const string BenefitAllowanceConflict = "PAYMENT_BENEFIT_DAILY_ALLOWANCE_CONFLICT";
    public const string BusinessRule = "PAYMENT_BUSINESS_RULE";
    public const string Unknown = "PAYMENT_ERROR";
}
