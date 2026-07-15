namespace KasseAPI_Final.Models;

/// <summary>RKSV report kinds persisted in <see cref="ReportPdf"/>.</summary>
public static class ReportPdfTypes
{
    public const string Tagesabschluss = "tagesabschluss";
    public const string Monatsbeleg = "monatsbeleg";
    public const string Jahresbeleg = "jahresbeleg";
    public const string Startbeleg = "startbeleg";
    public const string Nullbeleg = "nullbeleg";
    public const string Schlussbeleg = "schlussbeleg";
    public const string Receipt = "receipt";

    public static string FromClosingType(string? closingType) =>
        closingType?.Trim() switch
        {
            "Monthly" => Monatsbeleg,
            "Yearly" => Jahresbeleg,
            _ => Tagesabschluss,
        };

    public static string FromSpecialReceiptKind(string? kind) =>
        kind?.Trim() switch
        {
            RksvSpecialReceiptKinds.Startbeleg => Startbeleg,
            RksvSpecialReceiptKinds.Nullbeleg => Nullbeleg,
            RksvSpecialReceiptKinds.Schlussbeleg => Schlussbeleg,
            RksvSpecialReceiptKinds.Monatsbeleg => Monatsbeleg,
            RksvSpecialReceiptKinds.Jahresbeleg => Jahresbeleg,
            _ => Receipt,
        };

    public static bool IsKnown(string? reportType) =>
        reportType?.Trim().ToLowerInvariant() switch
        {
            Tagesabschluss or Monatsbeleg or Jahresbeleg or Startbeleg or Nullbeleg or Schlussbeleg or Receipt => true,
            _ => false,
        };

    public static string Normalize(string reportType) =>
        reportType.Trim().ToLowerInvariant();
}
