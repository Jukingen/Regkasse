namespace KasseAPI_Final.DTOs;

/// <summary>Returned when fiscal export requires prior UI disclaimer acknowledgment (§ 8 RKSV).</summary>
public sealed class FiscalExportDisclaimerRequiredResponseDto
{
    public string Error { get; set; } = "disclaimer_required";

    public string Message { get; set; } =
        "Sie müssen den rechtlichen Hinweis bestätigen, bevor Sie den Export herunterladen können.";

    public string EnglishMessage { get; set; } =
        "You must acknowledge the legal disclaimer before downloading this export.";

    public string DisclaimerUrl { get; set; } = FiscalExportDisclaimerPaths.RelativeDisclaimerUrl;
}
