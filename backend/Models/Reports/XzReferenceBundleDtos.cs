namespace KasseAPI_Final.Models.Reports;

/// <summary>
/// Named X/Z reference bundle: operational interim + full-day totals + database daily closings.
/// Not a hardware TSE-native X/Z artefact; see <see cref="LegalDisclaimers"/>.
/// </summary>
public sealed class XzReferenceBundleDto
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime ViennaBusinessDate { get; set; }
    public string ScopeKind { get; set; } = "Company";
    public Guid? CashRegisterId { get; set; }
    public string? CashierId { get; set; }
    public int? PaymentMethodFilter { get; set; }
    public bool ActiveOnly { get; set; }
    public bool IsCurrentBusinessDay { get; set; }

    /// <summary>Must-display legal / operator notices (English for audit consistency).</summary>
    public IReadOnlyList<string> LegalDisclaimers { get; set; } = Array.Empty<string>();

    /// <summary>Interim snapshot (X-like): only when <see cref="IsCurrentBusinessDay"/> is true.</summary>
    public InterimOperationalReportDto? InterimXLike { get; set; }

    /// <summary>Full Austria business day operational totals from payment_details (same filters).</summary>
    public OperationalSummaryDto FullDayOperationalSummary { get; set; } = new();

    public ClosingReferenceReportDto ClosingReference { get; set; } = new();

    /// <summary>IDs of daily_closing rows included in <see cref="ClosingReference"/>.</summary>
    public IReadOnlyList<Guid> LinkedClosingIds { get; set; } = Array.Empty<Guid>();

    /// <summary>Structured sections for UI labels (no duplicate fiscal semantics).</summary>
    public IReadOnlyList<XzReferenceBundlePartDto> Parts { get; set; } = Array.Empty<XzReferenceBundlePartDto>();

    public IReadOnlyList<string> InformationalWarnings { get; set; } = Array.Empty<string>();

    public XzInterimVsFullDayDto? InterimVsFullDaySnapshot { get; set; }
    public XzOperationalVsClosingDto? OperationalVsClosing { get; set; }
}

public sealed class XzReferenceBundlePartDto
{
    public string Kind { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class XzInterimVsFullDayDto
{
    public decimal InterimGrossTotal { get; set; }
    public decimal FullDayGrossTotal { get; set; }
    public decimal DeltaGross { get; set; }
}

public sealed class XzOperationalVsClosingDto
{
    public Guid PrimaryClosingId { get; set; }
    public decimal OperationalGrossTotal { get; set; }
    public decimal ClosingTotalAmount { get; set; }
    public decimal DeltaGross { get; set; }
    public string Note { get; set; } =
        "Operational totals derive from payment_details; closing totals derive from Tagesabschluss/daily_closing. Different definitions are expected.";
}
