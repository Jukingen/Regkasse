namespace KasseAPI_Final.DTOs;

/// <summary>Stable status codes for <see cref="ReceiptListItemDto.Status"/> (POS Belegliste).</summary>
public static class ReceiptListStatuses
{
    public const string Paid = "Paid";
    public const string Storno = "Storno";
    public const string Refund = "Refund";

    public static string FromPayment(bool isStorno, bool isRefund, string? rksvSpecialReceiptKind)
    {
        if (!string.IsNullOrWhiteSpace(rksvSpecialReceiptKind))
            return rksvSpecialReceiptKind.Trim();
        if (isStorno)
            return Storno;
        if (isRefund)
            return Refund;
        return Paid;
    }
}
