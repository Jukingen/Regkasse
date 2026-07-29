namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>FinanzOnline outbox message types for RKSV Ausfall / Wiederinbetriebnahme.</summary>
public static class FinanzOnlineRksvAusfallOutboxMessageTypes
{
    public const string RksvAusfallSeSubmission = "RksvAusfallSeSubmission";
    public const string RksvWiederinbetriebnahmeSeSubmission = "RksvWiederinbetriebnahmeSeSubmission";
    public const string RksvAusfallKasseSubmission = "RksvAusfallKasseSubmission";
    public const string RksvWiederinbetriebnahmeKasseSubmission = "RksvWiederinbetriebnahmeKasseSubmission";

    public static bool IsAusfallFamily(string? messageType) =>
        string.Equals(messageType, RksvAusfallSeSubmission, StringComparison.Ordinal) ||
        string.Equals(messageType, RksvWiederinbetriebnahmeSeSubmission, StringComparison.Ordinal) ||
        string.Equals(messageType, RksvAusfallKasseSubmission, StringComparison.Ordinal) ||
        string.Equals(messageType, RksvWiederinbetriebnahmeKasseSubmission, StringComparison.Ordinal);
}
