namespace KasseAPI_Final.Models;

/// <summary>
/// API/detail projection for <see cref="DailyClosing"/> with RKSV Phase 1 presentation fields.
/// </summary>
public class DailyClosingDto
{
    public Guid Id { get; set; }

    public Guid CashRegisterId { get; set; }

    public string? RegisterNumber { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? CashierName { get; set; }

    public DateTime ClosingDate { get; set; }

    public string ClosingType { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public decimal TotalTaxAmount { get; set; }

    public int TransactionCount { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? FinanzOnlineStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public string TseSignature { get; set; } = string.Empty;

    public string TseSignatureTimestamp { get; set; } = string.Empty;

    public string TseCertificateThumbprint { get; set; } = string.Empty;

    public string PreviousSignature { get; set; } = string.Empty;

    public int SignatureChainLength { get; set; }

    public bool IsSimulated { get; set; }

    public string Environment { get; set; } = string.Empty;

    /// <summary>Long-form TSE status for report/detail rows (e.g. TSE: AKTIV).</summary>
    public string TseStatusDisplay { get; set; } = string.Empty;

    /// <summary>Multi-line RKSV footer block for print/PDF.</summary>
    public string RksvFooter { get; set; } = string.Empty;
}
