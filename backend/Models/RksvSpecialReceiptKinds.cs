namespace KasseAPI_Final.Models;

/// <summary>
/// Persisted in <c>payment_details.rksv_special_receipt_kind</c>. NULL = normal sale/refund row.
/// </summary>
public static class RksvSpecialReceiptKinds
{
    public const string Nullbeleg = "Nullbeleg";

    /// <summary>RKSV first signed zero receipt after go-live / register commissioning (one per register).</summary>
    public const string Startbeleg = "Startbeleg";

    /// <summary>RKSV monthly zero receipt (one per register per Vienna calendar month; <see cref="PaymentDetails.RksvSpecialReceiptYear"/> / Month).</summary>
    public const string Monatsbeleg = "Monatsbeleg";

    /// <summary>RKSV annual zero receipt (one per register per Vienna calendar year; December Monatsbeleg flow maps here).</summary>
    public const string Jahresbeleg = "Jahresbeleg";

    /// <summary>RKSV Schlussbeleg / Endbeleg — final zero receipt when permanently decommissioning a cash register (one per register).</summary>
    public const string Schlussbeleg = "Schlussbeleg";
}
