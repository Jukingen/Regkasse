using System.Net;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tse.Fiskaly;

namespace KasseAPI_Final.Services;

/// <summary>Maps Fiskaly/TSE failures to the SuperAdmin receipt error envelope (code, message, details).</summary>
public static class FiskalyReceiptErrorMapper
{
    public static FiskalyReceiptErrorDto FromException(FiskalyApiException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var code = ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? FiskalyReceiptErrorCodes.FiskalyAuthFailed
            : FiskalyReceiptErrorCodes.FiskalyApiError;

        return new FiskalyReceiptErrorDto
        {
            Code = code,
            Message = ex.Message,
            Details = BuildDetails(ex)
        };
    }

    public static FiskalyReceiptErrorDto FromTseUnavailable(TseUnavailableException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (ex.InnerException is FiskalyApiException fiskaly)
        {
            var inner = FromException(fiskaly);
            return new FiskalyReceiptErrorDto
            {
                Code = inner.Code,
                Message = ex.Message,
                Details = inner.Details ?? inner.Message
            };
        }

        return new FiskalyReceiptErrorDto
        {
            Code = FiskalyReceiptErrorCodes.TseUnavailable,
            Message = ex.Message,
            Details = ex.InnerException?.Message
        };
    }

    public static FiskalyReceiptErrorDto FromCode(string code, string message, string? details = null) =>
        new()
        {
            Code = string.IsNullOrWhiteSpace(code) ? FiskalyReceiptErrorCodes.FiskalyApiError : code,
            Message = message,
            Details = details
        };

    public static string InferCodeFromMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return FiskalyReceiptErrorCodes.FiskalyApiError;

        if (message.Contains("disabled", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.FiskalyDisabled;
        if (message.Contains("not configured", StringComparison.OrdinalIgnoreCase)
            || message.Contains("credentials", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.FiskalyNotConfigured;
        if (message.Contains("LIVE", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.FiskalyLiveBlocked;
        if (message.Contains("not INITIALIZED", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.FiskalyRegisterNotInitialized;
        if (message.Contains("not registered at fiskaly", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.FiskalyRegisterNotFound;
        if (message.Contains("Cash register not found", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.CashRegisterNotFound;
        if (message.Contains("Cash register id is required", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.CashRegisterIdRequired;
        if (message.Contains("Startbeleg is required", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.StartbelegRequired;
        if (message.Contains("completed (past) Vienna calendar months", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.PeriodNotCompleted;
        if (message.Contains("Year must be", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Month must be", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Vienna year", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.InvalidPeriod;
        if (message.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || message.Contains("401", StringComparison.OrdinalIgnoreCase)
            || message.Contains("403", StringComparison.OrdinalIgnoreCase))
            return FiskalyReceiptErrorCodes.FiskalyAuthFailed;

        return FiskalyReceiptErrorCodes.FiskalyApiError;
    }

    private static string? BuildDetails(FiskalyApiException ex)
    {
        var parts = new List<string>(3);
        if (ex.StatusCode is { } status)
            parts.Add($"HTTP {(int)status}");
        if (!string.IsNullOrWhiteSpace(ex.RequestId))
            parts.Add($"request-id={ex.RequestId}");
        if (!string.IsNullOrWhiteSpace(ex.Environment))
            parts.Add($"env={ex.Environment}");
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }
}
