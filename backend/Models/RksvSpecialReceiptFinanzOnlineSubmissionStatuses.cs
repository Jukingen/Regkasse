namespace KasseAPI_Final.Models;

/// <summary>
/// FinanzOnline / BMF submission lifecycle for RKSV Startbeleg and Jahresbeleg (no external calls in this phase).
/// </summary>
public static class RksvSpecialReceiptFinanzOnlineSubmissionStatuses
{
    public const string NotRequired = "NotRequired";
    public const string Pending = "Pending";
    public const string Submitted = "Submitted";
    public const string Verified = "Verified";
    public const string Failed = "Failed";
    public const string ManualVerificationRequired = "ManualVerificationRequired";
}
