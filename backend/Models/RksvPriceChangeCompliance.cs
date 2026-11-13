namespace KasseAPI_Final.Models;

/// <summary>
/// Result of an RKSV compliance check for a proposed product price / tax-group change.
/// Named distinctly from backup <c>ComplianceResult</c> and catalog <see cref="ComplianceReport"/>.
/// </summary>
public sealed class RksvPriceChangeComplianceResult
{
    public bool IsCompliant { get; set; }

    public List<RksvComplianceFinding> Warnings { get; set; } = [];

    public List<RksvComplianceFinding> Errors { get; set; } = [];

    public List<RksvComplianceFinding> Requirements { get; set; } = [];

    /// <summary>True when the product appears on prior sales lines (order history).</summary>
    public bool HasFiscalHistory { get; set; }

    /// <summary>True when the change must create a new catalog product version.</summary>
    public bool RequiresNewProductVersion { get; set; }

    public decimal? NewTaxRate { get; set; }
}

/// <summary>Machine-readable RKSV finding (warning, error, or requirement).</summary>
public sealed class RksvComplianceFinding
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Resolution { get; set; } = string.Empty;
}
