using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

public static class PaymentReversalReasonMapper
{
    public static StornoReason ToStornoReason(CancellationReasonCode code) =>
        code switch
        {
            CancellationReasonCode.CustomerRequest => StornoReason.KundeStorniert,
            CancellationReasonCode.PriceMismatch => StornoReason.FalscherBetrag,
            CancellationReasonCode.TechnicalError => StornoReason.TechnischerFehler,
            CancellationReasonCode.WrongItem => StornoReason.Anderes,
            CancellationReasonCode.Duplicate => StornoReason.TechnischerFehler,
            CancellationReasonCode.Other => StornoReason.Anderes,
            _ => StornoReason.Anderes
        };

    public static string FormatRefundReason(RefundReasonCode code, string reason)
    {
        var label = code switch
        {
            RefundReasonCode.CustomerComplaint => "CustomerComplaint",
            RefundReasonCode.WrongProduct => "WrongProduct",
            RefundReasonCode.QualityIssue => "QualityIssue",
            RefundReasonCode.Overcharged => "Overcharged",
            RefundReasonCode.Other => "Other",
            _ => "Other"
        };
        return $"[{label}] {reason.Trim()}";
    }

    public static string FormatCancellationReason(CancellationReasonCode code, string reason)
    {
        var label = code.ToString();
        return $"[{label}] {reason.Trim()}";
    }
}
