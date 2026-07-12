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
            Cashier = baseLabels.Cashier,
            TseStatus = baseLabels.TseStatus,
            TotalSales = baseLabels.TotalSales,
            TotalCash = baseLabels.TotalCash,
            TotalCard = baseLabels.TotalCard,
            TotalVoucher = baseLabels.TotalVoucher,
            TotalOther = baseLabels.TotalOther,
            PaymentMethodsSection = baseLabels.PaymentMethodsSection,
            TransactionBreakdownSection = baseLabels.TransactionBreakdownSection,
            BreakdownCash = baseLabels.BreakdownCash,
            BreakdownCard = baseLabels.BreakdownCard,
            BreakdownVoucher = baseLabels.BreakdownVoucher,
            BreakdownCancellations = baseLabels.BreakdownCancellations,
            BreakdownTotal = baseLabels.BreakdownTotal,
            CashCount = baseLabels.CashCount,
            Difference = baseLabels.Difference,
            FiscalTotal = baseLabels.FiscalTotal,
            FiscalTax = baseLabels.FiscalTax,
            TaxSection = baseLabels.TaxSection,
            Tax20 = baseLabels.Tax20,
            Tax10 = baseLabels.Tax10,
            Tax0 = baseLabels.Tax0,
            PreviousSignature = baseLabels.PreviousSignature,
            Transactions = baseLabels.Transactions,
            TseSignature = baseLabels.TseSignature,
            Disclaimer = ResolveDisclaimer(NormalizeLanguage(language), closingType)
                         ?? ResolveDailyDisclaimer(NormalizeLanguage(language)),
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
            "en" => "RKSV-compliant period closing with TSE signature — retain for audit.",
            "tr" => "TSE imzalı RKSV uyumlu dönem kapanışı — denetim için saklayın.",
            _ => DailyClosingReportComposer.RksvPeriodDisclaimerDe,
        };
    }

    private static string ResolveDailyDisclaimer(string language) =>
        language switch
        {
            "en" => DailyClosingReportComposer.RksvDailyDisclaimerEn,
            "tr" => DailyClosingReportComposer.RksvDailyDisclaimerTr,
            _ => DailyClosingReportComposer.RksvDailyDisclaimerDe,
        };

    public static DailyClosingReportLabels Resolve(string? language) => Resolve(language, "Daily");

    public static DailyClosingReportLabels GermanDailyReport { get; } = new()
    {
        Title = "Tagesabschluss-Bericht",
        Date = "Datum",
        Register = "Kasse",
        Cashier = "Kassierer",
        TseStatus = "TSE-Status",
        TotalSales = "Umsatz",
        TotalCash = "Bar",
        TotalCard = "Karte",
        TotalVoucher = "Gutschein",
        TotalOther = "Sonstige",
        PaymentMethodsSection = "Zahlungsarten",
        TransactionBreakdownSection = "Transaktionen (Anzahl)",
        BreakdownCash = "Bar",
        BreakdownCard = "Karte",
        BreakdownVoucher = "Gutschein",
        BreakdownCancellations = "Stornos",
        BreakdownTotal = "Gesamt",
        CashCount = "Gezähltes Bargeld",
        Difference = "Differenz",
        FiscalTotal = "Fiskal Brutto",
        FiscalTax = "Fiskal MwSt.",
        TaxSection = "MwSt.-Aufschlüsselung",
        Tax20 = "20 % MwSt.",
        Tax10 = "10 % MwSt.",
        Tax0 = "0 % MwSt.",
        PreviousSignature = "Vorherige TSE-Signatur",
        Transactions = "Transaktionen",
        TseSignature = "TSE-Signatur",
        Disclaimer = DailyClosingReportComposer.RksvDailyDisclaimerDe,
    };

    public static DailyClosingReportLabels EnglishDailyReport { get; } = new()
    {
        Title = "Daily Closing Report",
        Date = "Date",
        Register = "Register",
        Cashier = "Cashier",
        TseStatus = "TSE status",
        TotalSales = "Sales",
        TotalCash = "Cash",
        TotalCard = "Card",
        TotalVoucher = "Voucher",
        TotalOther = "Other",
        PaymentMethodsSection = "Payment methods",
        TransactionBreakdownSection = "Transactions (count)",
        BreakdownCash = "Cash",
        BreakdownCard = "Card",
        BreakdownVoucher = "Voucher",
        BreakdownCancellations = "Cancellations",
        BreakdownTotal = "Total",
        CashCount = "Cash count",
        Difference = "Difference",
        FiscalTotal = "Fiscal gross",
        FiscalTax = "Fiscal tax",
        TaxSection = "VAT breakdown",
        Tax20 = "20% VAT",
        Tax10 = "10% VAT",
        Tax0 = "0% VAT",
        PreviousSignature = "Previous TSE signature",
        Transactions = "Transactions",
        TseSignature = "TSE signature",
        Disclaimer = DailyClosingReportComposer.RksvDailyDisclaimerEn,
    };

    public static DailyClosingReportLabels TurkishDailyReport { get; } = new()
    {
        Title = "Günlük Kapanış Raporu",
        Date = "Tarih",
        Register = "Kasa",
        Cashier = "Kasiyer",
        TseStatus = "TSE durumu",
        TotalSales = "Satış",
        TotalCash = "Nakit",
        TotalCard = "Kart",
        TotalVoucher = "Kupon",
        TotalOther = "Diğer",
        PaymentMethodsSection = "Ödeme türleri",
        TransactionBreakdownSection = "İşlemler (adet)",
        BreakdownCash = "Nakit",
        BreakdownCard = "Kart",
        BreakdownVoucher = "Kupon",
        BreakdownCancellations = "İptaller",
        BreakdownTotal = "Toplam",
        CashCount = "Sayılan nakit",
        Difference = "Fark",
        FiscalTotal = "Mali toplam",
        FiscalTax = "Mali KDV",
        TaxSection = "KDV dökümü",
        Tax20 = "%20 KDV",
        Tax10 = "%10 KDV",
        Tax0 = "%0 KDV",
        PreviousSignature = "Önceki TSE imzası",
        Transactions = "İşlemler",
        TseSignature = "TSE imzası",
        Disclaimer = DailyClosingReportComposer.RksvDailyDisclaimerTr,
    };
}
