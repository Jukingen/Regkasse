namespace KasseAPI_Final.Services;

/// <summary>
/// Fixed RKSV/Fiscal export disclaimers for internal diagnostics only (not § 8 RKSV legal proof).
/// </summary>
public sealed class DisclaimerService : IDisclaimerService
{
    /// <summary>German disclaimer — primary wording for Austrian operators.</summary>
    public const string RksvDisclaimerDe =
        "DIESER EXPORT IST KEIN RECHTSVERBINDLICHER FISKALBELEG NACH § 8 RKSV. Er dient ausschließlich der internen Analyse und Diagnostik. Für steuerliche Zwecke ist der Originalbeleg mit gültiger TSE-Signatur maßgeblich. Bei Anfragen des Finanzamts ist der Originalbeleg vorzulegen.";

    /// <summary>English disclaimer — for international administrators.</summary>
    public const string RksvDisclaimerEn =
        "THIS EXPORT IS NOT A LEGALLY BINDING FISCAL RECEIPT ACCORDING TO § 8 RKSV. It is for internal analysis and diagnostics only. For tax purposes, the original receipt with valid TSE signature is binding. For Finanzamt inquiries, the original receipt must be presented.";

    public string GetRksvDisclaimer(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return RksvDisclaimerDe;

        return language.Trim().ToLowerInvariant() switch
        {
            "en" => RksvDisclaimerEn,
            _ => RksvDisclaimerDe,
        };
    }
}
