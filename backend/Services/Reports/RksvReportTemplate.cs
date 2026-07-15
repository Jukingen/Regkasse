using System.Globalization;
using System.Text;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Canonical Cloud POS RKSV report names.</summary>
public static class RksvReportNames
{
    public const string Tagesabschluss = "Tagesabschluss";
    public const string Monatsbeleg = "Monatsbeleg";
    public const string Jahresbeleg = "Jahresbeleg";
    public const string Nullbeleg = "Nullbeleg";
    public const string Schlussbeleg = "Schlussbeleg";
    public const string Startbeleg = "Startbeleg";
    public const string Beleg = "Beleg";
    public const string StornoBeleg = "Stornobeleg";
    public const string Erstattungsbeleg = "Erstattungsbeleg";
}

/// <summary>Optional receipt line for unified plain-text RKSV reports.</summary>
public sealed class RksvReportLineItem
{
    public string Name { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPriceGross { get; init; }

    public decimal LineTotalGross { get; init; }
}

/// <summary>
/// Unified Cloud POS RKSV report model (thermal/POS plain text and shared PDF metadata).
/// Omits on-premise EFR/hardware fields.
/// </summary>
public sealed class RksvReportTemplate
{
    public string CompanyName { get; set; } = string.Empty;

    public string CompanyAddress { get; set; } = string.Empty;

    public string CompanyVatId { get; set; } = string.Empty;

    public string ReportName { get; set; } = RksvReportNames.Beleg;

    public string CashRegisterId { get; set; } = string.Empty;

    public string? RegisterNumber { get; set; }

    public DateTime? PeriodStart { get; set; }

    public DateTime? PeriodEnd { get; set; }

    public DateTime? DocumentDate { get; set; }

    public decimal TotalGross { get; set; }

    public decimal TotalNet { get; set; }

    public decimal TaxAmount { get; set; }

    public int TransactionCount { get; set; }

    public string? CashierName { get; set; }

    /// <summary>RKSV operational — Schicht-Nr. (short shift id).</summary>
    public string? ShiftNumber { get; set; }

    public string? TseSignature { get; set; }

    public string? TseSignatureTimestamp { get; set; }

    public string TseProvider { get; set; } = "fiskaly Cloud-HSM";

    public bool IsSimulated { get; set; }

    public bool TseSignatureVerified { get; set; }

    public bool HasStartbeleg { get; set; }

    public bool HasMonatsbeleg { get; set; }

    public bool HasJahresbeleg { get; set; }

    public string? DepExportStatus { get; set; }

    public string? FinanzOnlineStatus { get; set; }

    public string QrCode { get; set; } = string.Empty;

    public string RksvFooter { get; set; } = string.Empty;

    public string? EnvironmentDisplay { get; set; }

    public string? ReceiptNumber { get; set; }

    public decimal? CashTotal { get; set; }

    public decimal? CardTotal { get; set; }

    public decimal? VoucherTotal { get; set; }

    public decimal? TaxRate20 { get; set; }

    public decimal? TaxRate10 { get; set; }

    public decimal? TaxRate0 { get; set; }

    public IReadOnlyList<RksvReportLineItem>? LineItems { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Renders <see cref="RksvReportTemplate"/> as RKSV plain-text thermal output.</summary>
public static class RksvReportTemplateRenderer
{
    private static readonly CultureInfo DeAt = CultureInfo.GetCultureInfo("de-AT");

    public static string Render(RksvReportTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var cashier = string.IsNullOrWhiteSpace(template.CashierName) ? "—" : template.CashierName.Trim();
        var shiftNumber = RksvShiftNumberFormatter.FormatOrDash(template.ShiftNumber);
        var companyName = string.IsNullOrWhiteSpace(template.CompanyName) ? "—" : template.CompanyName.Trim();
        var companyAddress = string.IsNullOrWhiteSpace(template.CompanyAddress) ? "—" : template.CompanyAddress.Trim();
        var companyVatId = string.IsNullOrWhiteSpace(template.CompanyVatId) ? "—" : template.CompanyVatId.Trim();
        var envSuffix = string.IsNullOrWhiteSpace(template.EnvironmentDisplay)
            ? string.Empty
            : $" - {template.EnvironmentDisplay.Trim()}";
        var documentLocal = template.DocumentDate.HasValue
            ? PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(template.DocumentDate.Value)
            : PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(template.GeneratedAt);

        var builder = new StringBuilder();
        builder.AppendLine("═══════════════════════════════════════════");
        builder.AppendLine($"  Firmenname:      {companyName}");
        builder.AppendLine($"  Firmenadresse:   {companyAddress}");
        builder.AppendLine($"  UID:             {companyVatId}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine($"  {template.ReportName.ToUpperInvariant()}{envSuffix}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine($"  Datum:           {documentLocal:dd.MM.yyyy HH:mm}");
        if (template.PeriodStart.HasValue || template.PeriodEnd.HasValue)
            builder.AppendLine($"  Zeitraum:        {FormatPeriod(template.PeriodStart, template.PeriodEnd)}");
        builder.AppendLine($"  Kassen-ID:       {template.CashRegisterId}");
        if (!string.IsNullOrWhiteSpace(template.RegisterNumber))
            builder.AppendLine($"  Kasse:           {template.RegisterNumber.Trim()}");
        if (!string.IsNullOrWhiteSpace(template.ReceiptNumber))
            builder.AppendLine($"  Beleg-Nr.:       {template.ReceiptNumber.Trim()}");
        builder.AppendLine($"  Mitarbeiter:     {cashier}");
        builder.AppendLine($"  Schicht-Nr.:     {shiftNumber}");
        builder.AppendLine($"  TSE:             {template.TseProvider}");
        if (!string.IsNullOrWhiteSpace(template.DepExportStatus))
            builder.AppendLine($"  DEP-Export:      {template.DepExportStatus.Trim()}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine("  Finanzübersicht:");
        builder.AppendLine($"    Brutto gesamt: {FormatMoney(template.TotalGross)}");
        builder.AppendLine($"    Netto gesamt:  {FormatMoney(template.TotalNet)}");
        builder.AppendLine($"    MwSt. gesamt:  {FormatMoney(template.TaxAmount)}");

        if (template.LineItems is { Count: > 0 })
        {
            builder.AppendLine("───────────────────────────────────────────");
            builder.AppendLine("  Positionen:");
            foreach (var item in template.LineItems)
            {
                builder.AppendLine(
                    $"    {item.Quantity} x {item.Name}  {FormatMoney(item.LineTotalGross)}");
            }
        }

        if (HasPaymentBreakdown(template))
        {
            builder.AppendLine("───────────────────────────────────────────");
            builder.AppendLine("  Zahlungsarten:");
            if (template.CashTotal.HasValue)
                builder.AppendLine($"    Bargeld:       {FormatMoney(template.CashTotal.Value)}");
            if (template.CardTotal.HasValue)
                builder.AppendLine($"    Karte:         {FormatMoney(template.CardTotal.Value)}");
            if (template.VoucherTotal.HasValue)
                builder.AppendLine($"    Gutschein:     {FormatMoney(template.VoucherTotal.Value)}");
        }

        if (HasTaxBreakdown(template))
        {
            builder.AppendLine("───────────────────────────────────────────");
            builder.AppendLine("  Steuern:");
            if (template.TaxRate20 is > 0m)
                builder.AppendLine($"    20%:           {FormatMoney(template.TaxRate20.Value)}");
            if (template.TaxRate10 is > 0m)
                builder.AppendLine($"    10%:           {FormatMoney(template.TaxRate10.Value)}");
            if (template.TaxRate0 is > 0m)
                builder.AppendLine($"    0%:            {FormatMoney(template.TaxRate0.Value)}");
        }

        if (template.TransactionCount > 0)
        {
            builder.AppendLine("───────────────────────────────────────────");
            builder.AppendLine($"  Transaktionen:   {template.TransactionCount}");
        }

        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine("  RKSV / FinanzOnline:");
        builder.AppendLine($"    Startbeleg:    {FormatBelegStatus(template.HasStartbeleg)}");
        builder.AppendLine($"    Monatsbeleg:   {FormatBelegStatus(template.HasMonatsbeleg)}");
        builder.AppendLine($"    Jahresbeleg:   {FormatBelegStatus(template.HasJahresbeleg)}");
        if (!string.IsNullOrWhiteSpace(template.FinanzOnlineStatus))
            builder.AppendLine($"    FinanzOnline:  {template.FinanzOnlineStatus.Trim()}");

        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine("  TSE-Signatur:");
        builder.AppendLine($"  {template.TseSignature ?? "—"}");
        if (!string.IsNullOrWhiteSpace(template.TseSignatureTimestamp))
            builder.AppendLine($"  Zeitstempel:     {template.TseSignatureTimestamp.Trim()}");
        builder.AppendLine($"  Verifizierung:   {FormatVerificationStatus(template)}");
        builder.AppendLine("───────────────────────────────────────────");
        if (!string.IsNullOrWhiteSpace(template.QrCode))
            builder.AppendLine($"  QR-Code: {template.QrCode.Trim()}");
        if (!string.IsNullOrWhiteSpace(template.RksvFooter))
            builder.AppendLine(template.RksvFooter);
        builder.AppendLine("═══════════════════════════════════════════");

        return builder.ToString();
    }

    private static bool HasPaymentBreakdown(RksvReportTemplate template) =>
        template.CashTotal is > 0m
        || template.CardTotal is > 0m
        || template.VoucherTotal is > 0m;

    private static bool HasTaxBreakdown(RksvReportTemplate template) =>
        template.TaxRate20 is > 0m
        || template.TaxRate10 is > 0m
        || template.TaxRate0 is > 0m;

    private static string FormatMoney(decimal amount) =>
        amount.ToString("C", DeAt);

    private static string FormatBelegStatus(bool exists) =>
        exists ? "Ja" : "Nein";

    private static string FormatVerificationStatus(RksvReportTemplate template) =>
        template.TseSignatureVerified
            ? "GEPRÜFT ✅"
            : template.IsSimulated
                ? "SIMULIERT"
                : "NICHT VERIFIZIERT";

    private static string FormatPeriod(DateTime? periodStartUtc, DateTime? periodEndUtc)
    {
        if (!periodStartUtc.HasValue || !periodEndUtc.HasValue)
            return "—";

        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(
            periodStartUtc.Value,
            PostgreSqlUtcDateTime.AustriaTimeZone);
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(
            periodEndUtc.Value,
            PostgreSqlUtcDateTime.AustriaTimeZone);

        return $"{startLocal:dd.MM.yyyy HH:mm} – {endLocal:dd.MM.yyyy HH:mm}";
    }
}
