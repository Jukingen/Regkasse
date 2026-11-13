namespace KasseAPI_Final.Models.Reports;

/// <summary>VAT bucket totals for RKSV daily closing reports (20% / 10% / 0%).</summary>
public sealed class DailyClosingTaxBreakdownDto
{
    public decimal GrossAt20 { get; set; }

    public decimal TaxAt20 { get; set; }

    public decimal GrossAt10 { get; set; }

    public decimal TaxAt10 { get; set; }

    public decimal GrossAt0 { get; set; }

    /// <summary>Optional 13% special-rate bucket when present in fiscal data.</summary>
    public decimal GrossAt13 { get; set; }

    public decimal TaxAt13 { get; set; }
}
