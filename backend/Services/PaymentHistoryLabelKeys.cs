using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>Stable i18n keys returned by payment history API (resolved on the POS client by locale).</summary>
public static class PaymentHistoryLabelKeys
{
    public static class Actions
    {
        public const string Storno = "paymentHistory.actions.storno";
        public const string Refund = "paymentHistory.actions.refund";
        public const string View = "paymentHistory.actions.view";
    }

    public static class ReasonFields
    {
        public const string StornoTitle = "paymentHistory.reasons.stornoTitle";
        public const string RefundTitle = "paymentHistory.reasons.refundTitle";
    }

    public static class Reasons
    {
        public const string CustomerRequest = "paymentHistory.reasons.customerRequest";
        public const string WrongItem = "paymentHistory.reasons.wrongItem";
        public const string PriceMismatch = "paymentHistory.reasons.priceMismatch";
        public const string Duplicate = "paymentHistory.reasons.duplicate";
        public const string TechnicalError = "paymentHistory.reasons.technicalError";
        public const string CustomerComplaint = "paymentHistory.reasons.customerComplaint";
        public const string WrongProduct = "paymentHistory.reasons.wrongProduct";
        public const string QualityIssue = "paymentHistory.reasons.qualityIssue";
        public const string Overcharged = "paymentHistory.reasons.overcharged";
        public const string Other = "paymentHistory.reasons.other";
    }

    public static IReadOnlyList<ReasonOption> StornoReasonOptions { get; } =
    [
        new() { Code = "CUSTOMER_REQUEST", LabelKey = Reasons.CustomerRequest },
        new() { Code = "WRONG_ITEM", LabelKey = Reasons.WrongItem },
        new() { Code = "PRICE_MISMATCH", LabelKey = Reasons.PriceMismatch },
        new() { Code = "DUPLICATE", LabelKey = Reasons.Duplicate },
        new() { Code = "TECHNICAL_ERROR", LabelKey = Reasons.TechnicalError },
        new() { Code = "OTHER", LabelKey = Reasons.Other },
    ];

    public static IReadOnlyList<ReasonOption> RefundReasonOptions { get; } =
    [
        new() { Code = "CUSTOMER_COMPLAINT", LabelKey = Reasons.CustomerComplaint },
        new() { Code = "WRONG_PRODUCT", LabelKey = Reasons.WrongProduct },
        new() { Code = "QUALITY_ISSUE", LabelKey = Reasons.QualityIssue },
        new() { Code = "OVERCHARGED", LabelKey = Reasons.Overcharged },
        new() { Code = "OTHER", LabelKey = Reasons.Other },
    ];
}
