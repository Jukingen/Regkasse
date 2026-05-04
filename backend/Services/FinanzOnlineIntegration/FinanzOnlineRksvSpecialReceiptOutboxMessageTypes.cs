namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>FinanzOnline outbox message types for RKSV special receipt FO pipeline (submission/verification stub until real SOAP is wired).</summary>
public static class FinanzOnlineRksvSpecialReceiptOutboxMessageTypes
{
    public const string RksvStartbelegSubmission = "RksvStartbelegSubmission";
    public const string RksvJahresbelegSubmission = "RksvJahresbelegSubmission";
}
