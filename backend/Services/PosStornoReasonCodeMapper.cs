using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

public static class PosStornoReasonCodeMapper
{
    public static CancellationReasonCode Map(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return CancellationReasonCode.Other;

        var normalized = code.Trim();
        if (Enum.TryParse<CancellationReasonCode>(normalized, ignoreCase: true, out var enumCode))
            return enumCode;

        return normalized.ToUpperInvariant() switch
        {
            "CUSTOMER_REQUEST" => CancellationReasonCode.CustomerRequest,
            "WRONG_ITEM" => CancellationReasonCode.WrongItem,
            "PRICE_MISMATCH" => CancellationReasonCode.PriceMismatch,
            "DUPLICATE" => CancellationReasonCode.Duplicate,
            "TECHNICAL_ERROR" => CancellationReasonCode.TechnicalError,
            "OTHER" => CancellationReasonCode.Other,
            _ => CancellationReasonCode.Other,
        };
    }
}
