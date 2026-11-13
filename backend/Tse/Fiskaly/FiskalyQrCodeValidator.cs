using System.Globalization;
using System.Text.RegularExpressions;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>
/// Lenient RKSV machine-code check for fiskaly SIGN AT QR payloads (<c>_R1-AT1_</c> / <c>_R1-AT3_</c>).
/// Does not require the internal BelegNr pattern used by local PaymentService receipts.
/// </summary>
public static class FiskalyQrCodeValidator
{
    private static readonly Regex AmountRegex = new(
        @"^-?\d+([.,]\d{1,2})?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TimestampRegex = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static FiskalyQrValidationDto Validate(string? qrCodeData)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(qrCodeData))
        {
            errors.Add("QR code data is empty.");
            return new FiskalyQrValidationDto { IsValid = false, Errors = errors };
        }

        var trimmed = qrCodeData.Trim();
        if (!trimmed.StartsWith("_R1-AT", StringComparison.Ordinal))
        {
            errors.Add("QR payload must start with '_R1-AT' (RKSV machine-readable prefix).");
            return new FiskalyQrValidationDto { IsValid = false, Errors = errors, Prefix = trimmed.Length >= 7 ? trimmed[..7] : trimmed };
        }

        var parts = trimmed.Split('_', StringSplitOptions.None)
            .Where(p => p.Length > 0)
            .ToArray();

        // R1-ATx, Kassen-ID, BelegNr, timestamp, 5 amounts, AES counter, cert serial, prev sig, signature
        if (parts.Length < 13)
            errors.Add($"QR payload has {parts.Length} segments; expected at least 13 RKSV fields.");

        string? prefix = parts.Length > 0 ? parts[0] : null;
        string? serial = parts.Length > 1 ? parts[1] : null;
        string? receiptNumber = parts.Length > 2 ? parts[2] : null;
        string? timestamp = parts.Length > 3 ? parts[3] : null;

        if (string.IsNullOrWhiteSpace(serial))
            errors.Add("Cash register serial segment is empty.");
        if (string.IsNullOrWhiteSpace(receiptNumber))
            errors.Add("Receipt number segment is empty.");
        if (string.IsNullOrWhiteSpace(timestamp))
            errors.Add("Timestamp segment is empty.");
        else if (!TimestampRegex.IsMatch(timestamp) &&
                 !DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            errors.Add("Timestamp segment is not a valid ISO-8601 value.");
        }

        if (parts.Length >= 9)
        {
            for (var i = 4; i <= 8; i++)
            {
                if (!AmountRegex.IsMatch(parts[i]))
                    errors.Add($"Tax bucket segment {i - 3} is not a decimal amount.");
            }
        }

        if (parts.Length >= 13 && string.IsNullOrWhiteSpace(parts[^1]))
            errors.Add("Signature segment is empty.");

        return new FiskalyQrValidationDto
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Prefix = prefix,
            CashRegisterSerial = serial,
            ReceiptNumber = receiptNumber,
            Timestamp = timestamp
        };
    }
}
