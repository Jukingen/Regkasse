using System.Globalization;
using System.Text.RegularExpressions;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Rksv;

namespace KasseAPI_Final.Services;

/// <summary>
/// Strict minimal parser for QR strings produced by <see cref="PaymentService"/> / <see cref="ReceiptService"/>:
/// <c>_R1-AT1_{kassenId}_{receiptNumber}_{timestamp}_{gross}_{secondAmt}_{certSerial}_{compactJws}</c>.
/// </summary>
public sealed class RksvReceiptQrPayloadFormatValidator : IRksvReceiptQrPayloadFormatValidator
{
    /// <summary>Prefix currently emitted by payment and receipt builders.</summary>
    public const string SupportedPrefix = "_R1-AT1_";

    private static readonly Regex ReceiptNumberRegex = new(
        @"^AT-(?<register>.+)-(?<ymd>\d{8})-(?<tail>[A-Za-z0-9]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DecimalSegmentRegex = new(
        @"^\d+(\.\d{1,2})?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public RksvValidateReceiptQrResponse Validate(string? qrPayload)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(qrPayload))
        {
            errors.Add("qrPayload is required.");
            return Fail(errors);
        }

        var trimmed = qrPayload.Trim();
        if (!trimmed.StartsWith("_R1-", StringComparison.Ordinal))
        {
            errors.Add("QR payload must start with '_R1-' (RKSV machine-readable prefix).");
            return Fail(errors);
        }

        if (!trimmed.StartsWith(SupportedPrefix, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported RKSV QR version or variant; expected prefix '{SupportedPrefix}'.");
            return Fail(errors);
        }

        var parseResult = RksvQrParser.Parse(trimmed);
        if (!parseResult.Success || parseResult.Payload == null)
        {
            errors.AddRange(parseResult.Errors);
            return Fail(errors);
        }

        var p = parseResult.Payload;
        if (p.Layout != RksvQrPayloadLayout.InternalCompact)
        {
            errors.Add("This validator only accepts the internal compact RKSV QR layout.");
            return Fail(errors);
        }

        if (string.IsNullOrWhiteSpace(p.ReceiptNumber))
            errors.Add("Receipt number segment is empty.");
        else if (!ReceiptNumberRegex.IsMatch(p.ReceiptNumber))
            errors.Add("Receipt number does not match expected BelegNr pattern (AT-{register}-{yyyyMMdd}-{sequence}).");

        if (string.IsNullOrWhiteSpace(p.Timestamp))
            errors.Add("Timestamp segment is empty.");
        else if (!DateTime.TryParseExact(
                     p.Timestamp,
                     "yyyy-MM-ddTHH:mm:ss",
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite,
                     out _))
            errors.Add("Timestamp must be ISO 8601 'yyyy-MM-ddTHH:mm:ss'.");

        var total1 = p.TaxBuckets.FirstOrDefault(b => b.Code == RksvQrParser.InternalCompactPrimaryCode)?.Amount ?? "";
        var total2 = p.TaxBuckets.FirstOrDefault(b => b.Code == RksvQrParser.InternalCompactSecondaryCode)?.Amount ?? "";

        if (string.IsNullOrWhiteSpace(total1))
            errors.Add("First total segment is empty.");
        else if (!DecimalSegmentRegex.IsMatch(total1.Trim()))
            errors.Add("First total segment must be a decimal with at most two fractional digits.");

        if (string.IsNullOrWhiteSpace(total2))
            errors.Add("Second total segment is empty.");
        else if (!DecimalSegmentRegex.IsMatch(total2.Trim()))
            errors.Add("Second total segment must be a decimal with at most two fractional digits.");

        if (string.IsNullOrWhiteSpace(p.CertificateSerial))
            errors.Add("Certificate serial segment is empty.");

        if (string.IsNullOrWhiteSpace(p.Signature))
            errors.Add("Signature (compact JWS) segment is missing.");

        if (errors.Count > 0)
            return Fail(errors);

        return new RksvValidateReceiptQrResponse
        {
            IsValidFormat = true,
            Parsed = new RksvValidateReceiptQrParsedDto
            {
                ReceiptNumber = p.ReceiptNumber,
                Timestamp = p.Timestamp,
                Totals = new RksvValidateReceiptQrTotalsDto
                {
                    GrossTotal = total1.Trim(),
                    SecondAmount = total2.Trim()
                },
                CertificateSerial = p.CertificateSerial.Trim(),
                PreviousSignature = p.PreviousSignature
            },
            Errors = new List<string>()
        };
    }

    private static RksvValidateReceiptQrResponse Fail(IReadOnlyList<string> errors) =>
        new()
        {
            IsValidFormat = false,
            Parsed = null,
            Errors = errors.ToList()
        };
}
