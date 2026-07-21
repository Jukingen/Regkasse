namespace KasseAPI_Final.DTOs;

/// <summary>
/// Nachdruck-Begründung: stabile Codes für Audit und Reporting (keine freie Text-Enum in DB).
/// </summary>
public static class ReceiptReprintReasonCodes
{
    public const string CustomerRequest = "CUSTOMER_REQUEST";
    public const string PrinterFailure = "PRINTER_FAILURE";
    public const string LegalOrAuditCopy = "LEGAL_OR_AUDIT_COPY";
    public const string CorrectionReference = "CORRECTION_REFERENCE";
    public const string Other = "OTHER";

    /// <summary>Alle gültigen Werte (Großbuchstaben, Unterstrich).</summary>
    public static readonly string[] All =
    {
        CustomerRequest,
        PrinterFailure,
        LegalOrAuditCopy,
        CorrectionReference,
        Other
    };

    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        var t = code.Trim();
        foreach (var a in All)
        {
            if (string.Equals(a, t, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
