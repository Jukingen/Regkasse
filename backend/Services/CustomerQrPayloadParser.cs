using System.Text.RegularExpressions;

namespace KasseAPI_Final.Services;

/// <summary>
/// Parses POS customer identification QR payloads (not RKSV receipt QR).
/// Supported: customer:{guid|email}, RK:C:{customerNumber}, RK:CU:{guid}, regkasse://customer/{number|guid}, plain customer number.
/// </summary>
public static class CustomerQrPayloadParser
{
    private static readonly Regex RegkasseCustomerUriRegex = new(
        @"^regkasse://customer/(?<token>[^/?#]+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static CustomerQrParseResult Parse(string? rawPayload)
    {
        var trimmed = (rawPayload ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            return CustomerQrParseResult.Invalid("QR payload is empty");

        if (Guid.TryParse(trimmed, out var directGuid))
            return CustomerQrParseResult.ById(directGuid);

        if (trimmed.StartsWith("customer:", StringComparison.OrdinalIgnoreCase))
        {
            var token = trimmed["customer:".Length..].Trim();
            if (string.IsNullOrEmpty(token))
                return CustomerQrParseResult.Invalid("Customer token missing in QR payload");
            if (Guid.TryParse(token, out var guid))
                return CustomerQrParseResult.ById(guid);
            if (token.Contains('@', StringComparison.Ordinal))
                return CustomerQrParseResult.ByEmail(token);
            return CustomerQrParseResult.Invalid("Unrecognized customer: QR token");
        }

        if (trimmed.StartsWith("RK:C:", StringComparison.OrdinalIgnoreCase))
        {
            var number = trimmed["RK:C:".Length..].Trim();
            return string.IsNullOrEmpty(number)
                ? CustomerQrParseResult.Invalid("Customer number missing in QR payload")
                : CustomerQrParseResult.ByNumber(number);
        }

        if (trimmed.StartsWith("RK:CU:", StringComparison.OrdinalIgnoreCase))
        {
            var idPart = trimmed["RK:CU:".Length..].Trim();
            return Guid.TryParse(idPart, out var guid)
                ? CustomerQrParseResult.ById(guid)
                : CustomerQrParseResult.Invalid("Invalid customer id in QR payload");
        }

        var uriMatch = RegkasseCustomerUriRegex.Match(trimmed);
        if (uriMatch.Success)
        {
            var token = Uri.UnescapeDataString(uriMatch.Groups["token"].Value);
            if (Guid.TryParse(token, out var guid))
                return CustomerQrParseResult.ById(guid);
            return CustomerQrParseResult.ByNumber(token);
        }

        // Fallback: treat as customer number when it looks like one (alphanumeric + dash/underscore).
        if (Regex.IsMatch(trimmed, @"^[A-Za-z0-9_-]{1,20}$"))
            return CustomerQrParseResult.ByNumber(trimmed);

        return CustomerQrParseResult.Invalid("Unrecognized customer QR format");
    }
}

public readonly record struct CustomerQrParseResult(
    bool Ok,
    string? CustomerNumber,
    Guid? CustomerId,
    string? Email,
    string? Error)
{
    public static CustomerQrParseResult ByNumber(string number) =>
        new(true, number, null, null, null);

    public static CustomerQrParseResult ById(Guid id) =>
        new(true, null, id, null, null);

    public static CustomerQrParseResult ByEmail(string email) =>
        new(true, null, null, email, null);

    public static CustomerQrParseResult Invalid(string error) =>
        new(false, null, null, null, error);
}
