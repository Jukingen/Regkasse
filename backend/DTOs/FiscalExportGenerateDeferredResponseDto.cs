namespace KasseAPI_Final.DTOs;

/// <summary>POST generate response when binary export is deferred to GET download/{id}.</summary>
public sealed class FiscalExportGenerateDeferredResponseDto
{
    public Guid ExportId { get; set; }
    public string Format { get; set; } = string.Empty;
    public string DisclaimerUrl { get; set; } = FiscalExportDisclaimerPaths.RelativeDisclaimerUrl;
}
