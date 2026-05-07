namespace KasseAPI_Final.Models.Export;

/// <summary>
/// Wrapper for JSON fiscal export: mandatory <see cref="LegalNotice"/> plus export payload(s).
/// </summary>
public sealed class FiscalExportJsonEnvelopeDto
{
    /// <summary>RKSV § 8 disclaimer: this file is not legally binding fiscal proof.</summary>
    public string LegalNotice { get; set; } = string.Empty;

    /// <summary>Export packages (typically one diagnostic/accounting slice).</summary>
    public IReadOnlyList<FiscalExportPackageDto> Exports { get; set; } = Array.Empty<FiscalExportPackageDto>();
}
