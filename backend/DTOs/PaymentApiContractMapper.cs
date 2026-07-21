using KasseAPI_Final.Middleware;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// Builds standardized payment v2 envelopes and error bodies from <see cref="PaymentResult"/> and HTTP context.
/// </summary>
public static class PaymentApiContractMapper
{
    public const string PaymentContractHeaderName = "X-Regkasse-Payment-Contract";
    public const string PaymentContractV2 = "v2";

    public static bool WantsV2Contract(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(PaymentContractHeaderName, out var values))
            return false;
        var v = values.ToString().Trim();
        return string.Equals(v, PaymentContractV2, StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetCorrelationId(HttpContext httpContext) =>
        httpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;

    public static PaymentApiEnvelope<PaymentCreateSuccessData> CreatePaymentSuccessEnvelope(
        PaymentResult result,
        object? sanitizedPayment,
        string? correlationId,
        string? idempotencyKeyEcho)
    {
        var data = new PaymentCreateSuccessData
        {
            PaymentId = result.PaymentId ?? result.Payment?.Id ?? Guid.Empty,
            Payment = result.NonFiscalOfflineQueued ? null : sanitizedPayment,
            InvoicePersisted = result.InvoicePersisted,
            Tse = result.NonFiscalOfflineQueued
                ? null
                : new PaymentCreateTseData
                {
                    Provider = result.TseProvider,
                    IsDemoFiscal = result.IsDemoFiscal,
                    QrPayload = result.QrPayload,
                    ReceiptNumber = result.Payment?.ReceiptNumber
                },
            TimeSyncWarning = result.TimeSyncWarning,
            NonFiscalOfflineQueued = result.NonFiscalOfflineQueued,
            OfflineTransactionId = result.OfflineTransactionId
        };

        return new PaymentApiEnvelope<PaymentCreateSuccessData>
        {
            Success = true,
            Message = result.Message,
            CorrelationId = correlationId,
            Data = data,
            Idempotency = new PaymentIdempotencyInfo
            {
                Replay = result.IdempotentReplay,
                Key = string.IsNullOrWhiteSpace(idempotencyKeyEcho) ? null : idempotencyKeyEcho
            }
        };
    }

    public static PaymentApiErrorBody CreatePaymentErrorBody(
        PaymentResult result,
        string? correlationId)
    {
        var code = MapToErrorCode(result);
        return new PaymentApiErrorBody
        {
            Code = code,
            Message = result.Message,
            CorrelationId = correlationId,
            FieldErrors = result.Errors.Count > 0
                ? new Dictionary<string, string[]> { ["_"] = result.Errors.ToArray() }
                : null,
            Context = new PaymentApiErrorContext
            {
                DiagnosticCode = result.DiagnosticCode,
                LegacyErrors = result.Errors.Count > 0 ? result.Errors : null
            }
        };
    }

    public static int MapToStatusCode(PaymentResult result)
    {
        if (!string.IsNullOrEmpty(result.DiagnosticCode) && result.DiagnosticCode == CashRegisterResolutionCodes.Forbidden)
            return StatusCodes.Status403Forbidden;
        if (!string.IsNullOrEmpty(result.DiagnosticCode) && result.DiagnosticCode == "BENEFIT_DAILY_ALLOWANCE_CONFLICT")
            return StatusCodes.Status409Conflict;
        return StatusCodes.Status400BadRequest;
    }

    /// <summary>Maps service diagnostic / situation to stable API code.</summary>
    public static string MapToErrorCode(PaymentResult result)
    {
        var d = result.DiagnosticCode;
        if (d == CashRegisterResolutionCodes.Forbidden)
            return PaymentApiErrorCodes.ForbiddenRegister;
        if (d == "BENEFIT_DAILY_ALLOWANCE_CONFLICT")
            return PaymentApiErrorCodes.BenefitAllowanceConflict;
        if (!string.IsNullOrEmpty(d))
        {
            if (d.Contains("TSE", StringComparison.OrdinalIgnoreCase) && result.Message.Contains("not connected", StringComparison.OrdinalIgnoreCase))
                return PaymentApiErrorCodes.TseNotConnected;
            if (d.Contains("TSE", StringComparison.OrdinalIgnoreCase) || result.Message.Contains("TSE", StringComparison.OrdinalIgnoreCase))
            {
                if (result.Message.Contains("not ready", StringComparison.OrdinalIgnoreCase))
                    return PaymentApiErrorCodes.TseNotReady;
            }
            return PaymentApiErrorCodes.BusinessRule;
        }
        return PaymentApiErrorCodes.BusinessRule;
    }

    public static IActionResult ValidationError(string message, Dictionary<string, string[]>? fieldErrors, string? correlationId)
    {
        var body = new PaymentApiErrorBody
        {
            Code = PaymentApiErrorCodes.Validation,
            Message = message,
            CorrelationId = correlationId,
            FieldErrors = fieldErrors
        };
        return new BadRequestObjectResult(body);
    }

    public static Dictionary<string, string[]>? ModelStateToFieldErrors(ModelStateDictionary modelState)
    {
        var d = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in modelState)
        {
            if (kv.Value.Errors.Count == 0)
                continue;
            d[kv.Key] = kv.Value.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? e.Exception?.Message ?? "Invalid" : e.ErrorMessage).ToArray();
        }
        return d.Count > 0 ? d : null;
    }

    public static IActionResult UnauthorizedError(string message, string? correlationId)
    {
        var body = new PaymentApiErrorBody
        {
            Code = PaymentApiErrorCodes.Unauthorized,
            Message = message,
            CorrelationId = correlationId
        };
        return new ObjectResult(body) { StatusCode = StatusCodes.Status401Unauthorized };
    }
}
