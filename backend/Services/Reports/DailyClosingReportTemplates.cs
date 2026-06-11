using System.Globalization;

namespace KasseAPI_Final.Services.Reports;

public static class DailyClosingReportTemplates
{
    public static string NormalizeLanguage(string? language)
    {
        var key = language?.Trim().ToLowerInvariant();
        return key switch
        {
            "en" or "en-us" or "en-gb" => "en",
            "tr" or "tr-tr" => "tr",
            _ => "de",
        };
    }

    public static CultureInfo GetCulture(string normalizedLanguage) =>
        normalizedLanguage switch
        {
            "en" => CultureInfo.GetCultureInfo("en-GB"),
            "tr" => CultureInfo.GetCultureInfo("tr-TR"),
            _ => CultureInfo.GetCultureInfo("de-AT"),
        };

    public static DailyClosingReportLabels Resolve(string? language)
    {
        return NormalizeLanguage(language) switch
        {
            "en" => EnglishDailyReport,
            "tr" => TurkishDailyReport,
            _ => GermanDailyReport,
        };
    }

    public static DailyClosingReportLabels GermanDailyReport { get; } = new()
    {
        Title = "Tagesabschluss-Bericht",
        Date = "Datum",
        Register = "Kasse",
        TotalSales = "Umsatz",
        TotalCash = "Bar",
        TotalCard = "Karte",
        CashCount = "Gezähltes Bargeld",
        Difference = "Differenz",
        FiscalTotal = "Fiskal Brutto",
        FiscalTax = "Fiskal MwSt.",
        Transactions = "Transaktionen",
        TseSignature = "TSE-Signatur",
        Disclaimer =
            "Übersicht aus Zahlungszeilen — kein Ersatz für den operativen Tagesabschluss oder formale RKSV-Berichte.",
    };

    public static DailyClosingReportLabels EnglishDailyReport { get; } = new()
    {
        Title = "Daily Closing Report",
        Date = "Date",
        Register = "Register",
        TotalSales = "Sales",
        TotalCash = "Cash",
        TotalCard = "Card",
        CashCount = "Cash count",
        Difference = "Difference",
        FiscalTotal = "Fiscal gross",
        FiscalTax = "Fiscal tax",
        Transactions = "Transactions",
        TseSignature = "TSE signature",
        Disclaimer =
            "Overview from payment rows — not a substitute for operational daily closing or formal RKSV reports.",
    };

    public static DailyClosingReportLabels TurkishDailyReport { get; } = new()
    {
        Title = "Günlük Kapanış Raporu",
        Date = "Tarih",
        Register = "Kasa",
        TotalSales = "Satış",
        TotalCash = "Nakit",
        TotalCard = "Kart",
        CashCount = "Sayılan nakit",
        Difference = "Fark",
        FiscalTotal = "Mali toplam",
        FiscalTax = "Mali KDV",
        Transactions = "İşlemler",
        TseSignature = "TSE imzası",
        Disclaimer =
            "Ödeme satırlarından özet — operasyonel günlük kapanış veya resmi RKSV raporlarının yerine geçmez.",
    };
}
