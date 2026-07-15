namespace KasseAPI_Final.Models.Reports;

/// <summary>Tenant/register context for Cloud POS Tagesabschluss reports.</summary>
public sealed class TagesabschlussCloudContext
{
    public string CompanyName { get; init; } = string.Empty;

    public string CompanyAddress { get; init; } = string.Empty;

    public string CompanyVatId { get; init; } = string.Empty;

    public string? RegisterNumber { get; init; }

    public string TseProviderLabel { get; init; } = string.Empty;

    public string DepExportStatusLabel { get; init; } = string.Empty;

    public DateTime? PeriodStartUtc { get; init; }

    public DateTime? PeriodEndUtc { get; init; }

    public bool HasStartbeleg { get; init; }

    public bool HasMonatsbeleg { get; init; }

    public bool HasJahresbeleg { get; init; }

    public bool TseSignatureVerified { get; init; }
}
