using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

/// <summary>Body for <c>POST /api/admin/rksv/evidence-bundle</c>. UTC range required to bound bundle size.</summary>
public sealed class RksvEvidenceBundleRequestDto
{
    /// <summary>Optional cash register filter; null/empty = include all registers (auditor-wide bundle).</summary>
    public Guid? CashRegisterId { get; set; }

    /// <summary>Inclusive lower bound on receipt issue time (UTC). Required.</summary>
    [Required]
    public DateTime FromUtc { get; set; }

    /// <summary>Exclusive upper bound on receipt issue time (UTC). Required and must be strictly greater than <see cref="FromUtc"/>.</summary>
    [Required]
    public DateTime ToUtc { get; set; }

    /// <summary>When true (default) the per-receipt items and tax lines are embedded in receipts.json.</summary>
    public bool IncludeReceiptItems { get; set; } = true;

    /// <summary>When true (default) the TSE signature log file is included in the bundle.</summary>
    public bool IncludeTseSignatureLog { get; set; } = true;
}

/// <summary>
/// In-memory description of a generated RKSV evidence bundle (zip bytes + suggested file name + manifest).
/// </summary>
public sealed class RksvEvidenceBundleResultDto
{
    public byte[] ZipBytes { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public RksvEvidenceBundleManifestDto Manifest { get; set; } = new();
}

/// <summary>Bundle metadata file written to <c>manifest.json</c> inside the zip.</summary>
public sealed class RksvEvidenceBundleManifestDto
{
    public string BundleVersion { get; set; } = "1.0";
    public DateTime GeneratedAtUtc { get; set; }
    public Guid? CashRegisterId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    public string GeneratedByUserId { get; set; } = "unknown";

    /// <summary>Diagnostic disclaimer (German). Mirrors compliance-report wording.</summary>
    public string LegalNoticeDe { get; set; } = string.Empty;

    /// <summary>Auditor-facing note (English).</summary>
    public string AuditorNoticeEn { get; set; } = string.Empty;

    public List<RksvEvidenceBundleFileEntryDto> Files { get; set; } = new();

    public RksvEvidenceBundleCountsDto Counts { get; set; } = new();
}

/// <summary>One file entry inside the bundle manifest.</summary>
public sealed class RksvEvidenceBundleFileEntryDto
{
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Rows { get; set; }
    public long SizeBytes { get; set; }
}

/// <summary>Quick row counts for the manifest summary block.</summary>
public sealed class RksvEvidenceBundleCountsDto
{
    public int PaymentDetailRows { get; set; }
    public int ReceiptRows { get; set; }
    public int SignatureChainStateRows { get; set; }
    public int TseSignatureRows { get; set; }
}

/// <summary>
/// Diagnostic RKSV compliance test report for one cash register or all registers within a UTC range.
/// Read-only audit/snapshot view. Not a legally binding RKSV proof — see <see cref="LegalNoticeDe"/>.
/// </summary>
public sealed class RksvComplianceReportDto
{
    /// <summary>UTC timestamp at which this report snapshot was generated.</summary>
    public DateTime GeneratedAtUtc { get; set; }

    /// <summary>Optional cash register id filter applied; null when the report covers all registers.</summary>
    public Guid? CashRegisterId { get; set; }

    /// <summary>UTC range filter actually applied (defaults: all-time when caller omits).</summary>
    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    /// <summary>High-level pass/fail counters for the five RKSV checks.</summary>
    public RksvComplianceReportSummaryDto Summary { get; set; } = new();

    /// <summary>1) Special receipts (Startbeleg / Monatsbeleg / Jahresbeleg / Schlussbeleg / Nullbeleg).</summary>
    public List<RksvComplianceSpecialReceiptDto> SpecialReceipts { get; set; } = new();

    /// <summary>2) Per-receipt signature chain continuity check, grouped by register and ordered by issue time.</summary>
    public List<RksvComplianceSignatureChainItemDto> SignatureChain { get; set; } = new();

    /// <summary>3) Receipt number sequence gaps detected per (register, day).</summary>
    public List<RksvComplianceSequenceGapDto> SequenceGaps { get; set; } = new();

    /// <summary>4) Fiscal payments / receipts that are missing the TSE signature value.</summary>
    public List<RksvComplianceTseSignatureMissingDto> TseSignatureMissing { get; set; } = new();

    /// <summary>5) QR payload format validation outcomes for receipts that carry a QR payload.</summary>
    public List<RksvComplianceQrValidationItemDto> QrPayloadValidation { get; set; } = new();

    /// <summary>Mandatory diagnostic disclaimer (German). Mirrors the wording used by fiscal export PDFs.</summary>
    public string LegalNoticeDe { get; set; } =
        "Dieser Bericht ist kein rechtsverbindlicher RKSV-/Finanzamt-Beleg. "
        + "Nur für interne Compliance- und Diagnose-Zwecke. Originalbeleg mit TSE-Signatur ist maßgeblich.";
}

/// <summary>Counters used by UI badges and PDF summary box.</summary>
public sealed class RksvComplianceReportSummaryDto
{
    public int RegistersCovered { get; set; }
    public int FiscalReceiptsScanned { get; set; }
    public int SpecialReceiptsCount { get; set; }

    public int SignatureChainBreaks { get; set; }
    public int SequenceGapCount { get; set; }
    public int TseSignatureMissingCount { get; set; }
    public int QrFormatInvalidCount { get; set; }
    public int QrFormatMissingCount { get; set; }

    /// <summary>Convenience flag: true when no chain breaks, sequence gaps, missing signatures, or invalid QRs were detected.</summary>
    public bool OverallPass { get; set; }
}

/// <summary>Statuses used across compliance items.</summary>
public static class RksvComplianceStatus
{
    public const string Pass = "Pass";
    public const string Warn = "Warn";
    public const string Fail = "Fail";
}

/// <summary>One row per Sonderbeleg (Nullbeleg / Startbeleg / Monatsbeleg / Jahresbeleg / Schlussbeleg).</summary>
public sealed class RksvComplianceSpecialReceiptDto
{
    public Guid PaymentId { get; set; }
    public Guid? ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>Persisted kind label from <c>payment_details.rksv_special_receipt_kind</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>True when the December Nullbeleg path is configured to act as Jahresbeleg (informational only).</summary>
    public bool NullbelegActsAsJahresbeleg { get; set; }

    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }

    /// <summary>Receipt issue timestamp (UTC) — primary RKSV timeline value.</summary>
    public DateTime? IssuedAtUtc { get; set; }

    /// <summary>Vienna calendar attribution captured at creation time (Monatsbeleg/Jahresbeleg only).</summary>
    public int? Year { get; set; }
    public int? Month { get; set; }

    /// <summary>True when a TSE signature is present on the receipt — Sonderbelege MUST be signed.</summary>
    public bool HasTseSignature { get; set; }
}

/// <summary>Chain integrity check for one receipt vs. the previous receipt of the same register.</summary>
public sealed class RksvComplianceSignatureChainItemDto
{
    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }

    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }

    /// <summary>Short prefix of the current receipt's signature (display only).</summary>
    public string? SignaturePrefix { get; set; }

    /// <summary>Short prefix of the previous-signature value stored on this receipt.</summary>
    public string? PrevSignaturePrefix { get; set; }

    /// <summary>Short prefix of the previous receipt's actual signature (for diff view).</summary>
    public string? ExpectedPrevSignaturePrefix { get; set; }

    /// <summary><see cref="RksvComplianceStatus.Pass"/> when chain matches; otherwise Warn/Fail.</summary>
    public string Status { get; set; } = RksvComplianceStatus.Pass;

    /// <summary>Human-readable issue, English (technical log style). Empty when <see cref="Status"/> is Pass.</summary>
    public string? Issue { get; set; }
}

/// <summary>One detected gap inside the AT-{register}-{yyyyMMdd}-{seq} numeric sequence on a given day.</summary>
public sealed class RksvComplianceSequenceGapDto
{
    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }

    /// <summary>UTC date of the receipt-number day segment (yyyyMMdd).</summary>
    public DateTime SequenceDateUtc { get; set; }

    public int ExpectedSequence { get; set; }

    /// <summary>Closest receipt number observed before the gap (may be null when day starts above 1).</summary>
    public string? PreviousReceiptNumber { get; set; }

    /// <summary>Closest receipt number observed after the gap.</summary>
    public string? NextReceiptNumber { get; set; }
}

/// <summary>Per-payment record where TSE signature is missing or empty.</summary>
public sealed class RksvComplianceTseSignatureMissingDto
{
    public Guid PaymentId { get; set; }
    public Guid? ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;

    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    /// <summary>Optional Sonderbeleg label when the row is a special receipt; null for normal sales.</summary>
    public string? SpecialReceiptKind { get; set; }

    /// <summary>Source flag: <c>payment_details.tse_signature</c> empty.</summary>
    public bool PaymentSignatureMissing { get; set; }

    /// <summary>Source flag: <c>receipts.signature_value</c> empty.</summary>
    public bool ReceiptSignatureMissing { get; set; }
}

/// <summary>Format-only QR payload validation result for a single receipt (no crypto verification).</summary>
public sealed class RksvComplianceQrValidationItemDto
{
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;

    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    /// <summary>True when QR payload is missing entirely on the receipt.</summary>
    public bool QrPayloadMissing { get; set; }

    /// <summary>True when QR payload was present and parsed to the internal <c>_R1-AT1_</c> format.</summary>
    public bool IsValidFormat { get; set; }

    /// <summary>Error messages from the format validator (empty when <see cref="IsValidFormat"/>).</summary>
    public List<string> Errors { get; set; } = new();
}
