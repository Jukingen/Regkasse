using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

/// <summary>Request body for RKSV receipt QR format validation (no crypto, no external calls).</summary>
public sealed class RksvValidateReceiptQrRequest
{
    /// <summary>Raw QR string as printed or returned by payment/receipt APIs.</summary>
    [Required]
    [MinLength(1)]
    public string QrPayload { get; set; } = string.Empty;
}

/// <summary>Structured parse result for a valid RKSV-style receipt QR payload (BMF §9 standard or legacy internal compact).</summary>
public sealed class RksvValidateReceiptQrParsedDto
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public RksvValidateReceiptQrTotalsDto Totals { get; set; } = new();
    public string CertificateSerial { get; set; } = string.Empty;
    /// <summary>Previous chain signature segment when present (BMF §9 layout).</summary>
    public string? PreviousSignature { get; set; }
}

public sealed class RksvValidateReceiptQrTotalsDto
{
    /// <summary>First gross amount segment (matches <c>TotalAmount</c> in QR builder).</summary>
    public string GrossTotal { get; set; } = string.Empty;
    /// <summary>Second amount segment (currently fixed <c>0.00</c> in builders).</summary>
    public string SecondAmount { get; set; } = string.Empty;
}

/// <summary>Format-only validation outcome for RKSV receipt QR payloads.</summary>
public sealed class RksvValidateReceiptQrResponse
{
    public bool IsValidFormat { get; set; }
    public RksvValidateReceiptQrParsedDto? Parsed { get; set; }
    public List<string> Errors { get; set; } = new();
}
