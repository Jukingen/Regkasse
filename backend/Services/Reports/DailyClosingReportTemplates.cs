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

    public static DailyClosingReportLabels Resolve(string? language, string? closingType = "Daily")
    {
        var baseLabels = NormalizeLanguage(language) switch
        {
            "en" => EnglishDailyReport,
            "tr" => TurkishDailyReport,
            _ => GermanDailyReport,
        };

        var title = ResolveTitle(NormalizeLanguage(language), closingType);
        return new DailyClosingReportLabels
        {
            Title = title,
            Date = baseLabels.Date,
            Register = baseLabels.Register,
            TotalSales = baseLabels.TotalSales,
            TotalCash = baseLabels.TotalCash,
            TotalCard = baseLabels.TotalCard,
            CashCount = baseLabels.CashCount,
            Difference = baseLabels.Difference,
            FiscalTotal = baseLabels.FiscalTotal,
            FiscalTax = baseLabels.FiscalTax,
            Transactions = baseLabels.Transactions,
            TseSignature = baseLabels.TseSignature,
            Disclaimer = ResolveDisclaimer(NormalizeLanguage(language), closingType) ?? baseLabels.Disclaimer,
        };
    }

    private static string ResolveTitle(string language, string? closingType)
    {
        var kind = closingType?.Trim();
        if (string.Equals(kind, "Monthly", StringComparison.OrdinalIgnoreCase))
        {
            return language switch
            {
                "en" => "Monthly Closing Report",
                "tr" => "Aylık Kapanış Raporu",
                _ => "Monatsabschluss-Bericht",
            };
        }

        if (string.Equals(kind, "Yearly", StringComparison.OrdinalIgnoreCase))
        {
            return language switch
            {
                "en" => "Yearly Closing Report",
                "tr" => "Yıllık Kapanış Raporu",
                _ => "Jahresabschluss-Bericht",
            };
        }

        return language switch
        {
            "en" => "Daily Closing Report",
            "tr" => "Günlük Kapanış Raporu",
            _ => "Tagesabschluss-Bericht",
        };
    }

    private static string? ResolveDisclaimer(string language, string? closingType)
    {
        var kind = closingType?.Trim();
        if (!string.Equals(kind, "Monthly", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(kind, "Yearly", StringComparison.OrdinalIgnoreCase))
            return null;

        return language switch
        {
            "en" => "RKSV period closing with TSE signature — retain for audit.",
            "tr" => "TSE imzalı RKSV dönem kapanışı — denetim için saklayın.",
            _ => "RKSV-Periodenabschluss mit TSE-Signatur — für die Betriebsprüfung aufbewahren.",
        };
    }

    public static DailyClosingReportLabels Resolve(string? language) => Resolve(language, "Daily");

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
