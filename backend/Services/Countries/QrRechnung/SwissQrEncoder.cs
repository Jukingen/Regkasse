using System.Globalization;

namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>
/// SIX QR-bill IG 2.3 data group. Address type S only. Line separator is LF.
/// </summary>
internal static class SwissQrEncoder
{
    public const string Header = "SPC";
    public const string Version = "0200";
    public const string CodingUtf8 = "1";
    public const string AddressTypeStructured = "S";
    public const string Trailer = "EPD";

    public static string Encode(QrRechnungPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var lines = new List<string>(32)
        {
            Header,
            Version,
            CodingUtf8,
            payload.Iban,
        };
        AppendParty(lines, payload.Creditor, required: true);
        AppendEmptyParty(lines);
        lines.Add(FormatAmount(payload.Amount));
        lines.Add(payload.Currency);
        if (payload.Debtor is null)
            AppendEmptyParty(lines);
        else
            AppendParty(lines, payload.Debtor, required: true);
        lines.Add(ToWire(payload.ReferenceType));
        lines.Add(payload.Reference ?? string.Empty);
        lines.Add(payload.AdditionalInfo ?? string.Empty);
        lines.Add(Trailer);
        return string.Join('\n', lines);
    }

    public static QrRechnungReferenceType ResolveReferenceType(string? reference, QrRechnungReferenceType? requested)
    {
        if (requested is QrRechnungReferenceType type)
            return type;

        if (string.IsNullOrWhiteSpace(reference))
            return QrRechnungReferenceType.Non;

        var trimmed = reference.Trim();
        if (trimmed.StartsWith("RF", StringComparison.OrdinalIgnoreCase))
            return QrRechnungReferenceType.Scor;

        if (trimmed.Length == 27 && trimmed.All(char.IsDigit))
            return QrRechnungReferenceType.Qrr;

        throw new ArgumentException(
            "QR-Rechnung reference must be QRR (27 digits), SCOR (RF…), or empty (NON).",
            nameof(reference));
    }

    public static void Validate(string iban, QrRechnungReferenceType referenceType, string? reference, string currency)
    {
        if (iban.Length != 21 || !Mod97IsValid(iban))
        {
            throw new ArgumentException(
                "QR-Rechnung IBAN must be a 21-character CH or LI IBAN with a valid mod-97 checksum.",
                nameof(iban));
        }

        var qrIban = IsQrIban(iban);
        var normalizedRef = string.IsNullOrWhiteSpace(reference) ? string.Empty : reference.Trim().ToUpperInvariant();

        if (string.Equals(currency, "EUR", StringComparison.Ordinal) && referenceType == QrRechnungReferenceType.Qrr)
        {
            throw new ArgumentException("EUR QR-bills cannot use QRR.", nameof(currency));
        }

        switch (referenceType)
        {
            case QrRechnungReferenceType.Qrr:
                if (!qrIban)
                    throw new ArgumentException("QRR requires a QR-IBAN (IID 30000–31999).", nameof(iban));
                if (!IsValidQrr(normalizedRef))
                    throw new ArgumentException("QRR must be 27 digits with a valid mod-10 check digit.", nameof(reference));
                break;
            case QrRechnungReferenceType.Scor:
                if (qrIban)
                    throw new ArgumentException("SCOR requires an ordinary CH/LI IBAN, not a QR-IBAN.", nameof(iban));
                if (!IsValidScor(normalizedRef))
                    throw new ArgumentException("SCOR must be an ISO 11649 RF creditor reference.", nameof(reference));
                break;
            case QrRechnungReferenceType.Non:
                if (qrIban)
                    throw new ArgumentException("NON requires an ordinary CH/LI IBAN, not a QR-IBAN.", nameof(iban));
                if (normalizedRef.Length > 0)
                    throw new ArgumentException("NON does not carry a structured reference.", nameof(reference));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(referenceType));
        }
    }

    public static bool IsQrIban(string iban)
    {
        if (iban.Length < 9 || !int.TryParse(iban.AsSpan(4, 5), NumberStyles.None, CultureInfo.InvariantCulture, out var iid))
            return false;
        return iid is >= 30000 and <= 31999;
    }

    public static string FormatAmount(decimal? amount)
    {
        if (amount is null)
            return string.Empty;
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "QR-Rechnung amount cannot be negative.");
        return amount.Value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public static string ToWire(QrRechnungReferenceType type) => type switch
    {
        QrRechnungReferenceType.Qrr => "QRR",
        QrRechnungReferenceType.Scor => "SCOR",
        QrRechnungReferenceType.Non => "NON",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    internal static bool Mod97IsValid(string iban)
    {
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var ch in rearranged)
        {
            if (ch is >= '0' and <= '9')
            {
                remainder = (remainder * 10 + (ch - '0')) % 97;
                continue;
            }

            if (ch is < 'A' or > 'Z')
                return false;

            var value = ch - 'A' + 10;
            remainder = (remainder * 10 + (value / 10)) % 97;
            remainder = (remainder * 10 + (value % 10)) % 97;
        }

        return remainder == 1;
    }

    internal static bool IsValidQrr(string reference)
    {
        if (reference.Length != 27 || !reference.All(char.IsDigit))
            return false;

        int[] table = [0, 9, 4, 6, 8, 2, 7, 1, 3, 5];
        var carry = 0;
        foreach (var ch in reference)
            carry = table[(carry + (ch - '0')) % 10];
        return carry == 0;
    }

    internal static bool IsValidScor(string reference)
    {
        if (reference.Length is < 5 or > 25 || !reference.StartsWith("RF", StringComparison.Ordinal))
            return false;
        return Mod97IsValid(reference);
    }

    private static void AppendParty(List<string> lines, QrRechnungParty party, bool required)
    {
        if (required && string.IsNullOrWhiteSpace(party.Name))
            throw new ArgumentException("QR-Rechnung party name is required.");
        var country = (party.CountryCode ?? string.Empty).Trim().ToUpperInvariant();
        if (country.Length != 2)
            throw new ArgumentException("QR-Rechnung address type S requires a 2-letter country.");

        lines.Add(AddressTypeStructured);
        lines.Add(party.Name.Trim());
        lines.Add(party.AddressLine1?.Trim() ?? string.Empty);
        lines.Add(party.AddressLine2?.Trim() ?? string.Empty);
        lines.Add(party.PostalCode?.Trim() ?? string.Empty);
        lines.Add(party.City?.Trim() ?? string.Empty);
        lines.Add(country);
    }

    private static void AppendEmptyParty(List<string> lines)
    {
        for (var i = 0; i < 7; i++)
            lines.Add(string.Empty);
    }
}
