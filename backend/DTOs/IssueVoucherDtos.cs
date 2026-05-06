using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

/// <summary>POS Mehrzweckgutschein issuance — RKSV non-fiscal sale; no TSE.</summary>
public sealed class IssueVoucherRequest
{
    [Required]
    [Range(0.01, 999_999.99)]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    [MinLength(3)]
    public string Currency { get; set; } = "EUR";

    [Required]
    public DateTime ValidFrom { get; set; }

    [Required]
    public DateTime ValidUntil { get; set; }

    public Guid? CustomerId { get; set; }
}

public sealed class IssueVoucherResponse
{
    public Guid VoucherId { get; init; }
    public string MaskedCode { get; init; } = string.Empty;
    /// <summary>Returned once — never persisted or logged server-side.</summary>
    public string FullCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}
