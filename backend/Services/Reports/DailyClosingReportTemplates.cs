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
            Period = baseLabels.Period,
            Register = baseLabels.Register,
            CashRegisterId = baseLabels.CashRegisterId,
            Cashier = baseLabels.Cashier,
            ShiftNumber = baseLabels.ShiftNumber,
            CompanyName = baseLabels.CompanyName,
            CompanyAddress = baseLabels.CompanyAddress,
            CompanyVatId = baseLabels.CompanyVatId,
            TseStatus = baseLabels.TseStatus,
            TseProvider = baseLabels.TseProvider,
            DepExport = baseLabels.DepExport,
            TotalSales = baseLabels.TotalSales,
            TotalCash = baseLabels.TotalCash,
            TotalCard = baseLabels.TotalCard,
            TotalVoucher = baseLabels.TotalVoucher,
            TotalOther = baseLabels.TotalOther,
            PaymentMethodsSection = baseLabels.PaymentMethodsSection,
            FinancialSummarySection = baseLabels.FinancialSummarySection,
            TransactionBreakdownSection = baseLabels.TransactionBreakdownSection,
            RksvStatusSection = baseLabels.RksvStatusSection,
            Startbeleg = baseLabels.Startbeleg,
            Monatsbeleg = baseLabels.Monatsbeleg,
            Jahresbeleg = baseLabels.Jahresbeleg,
            BreakdownCash = baseLabels.BreakdownCash,
            BreakdownCard = baseLabels.BreakdownCard,
            BreakdownVoucher = baseLabels.BreakdownVoucher,
            BreakdownCancellations = baseLabels.BreakdownCancellations,
            BreakdownTotal = baseLabels.BreakdownTotal,
            CashCount = baseLabels.CashCount,
            Difference = baseLabels.Difference,
            FiscalTotal = baseLabels.FiscalTotal,
            FiscalNet = baseLabels.FiscalNet,
            FiscalTax = baseLabels.FiscalTax,
            TaxSection = baseLabels.TaxSection,
            Tax20 = baseLabels.Tax20,
            Tax10 = baseLabels.Tax10,
            Tax0 = baseLabels.Tax0,
            PreviousSignature = baseLabels.PreviousSignature,
            Transactions = baseLabels.Transactions,
            TseSignature = baseLabels.TseSignature,
            TseVerification = baseLabels.TseVerification,
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
        Period = "Zeitraum",
        Register = "Kasse",
        CashRegisterId = "Kassen-ID",
        Cashier = "Mitarbeiter",
        ShiftNumber = "Schicht-Nr.",
        CompanyName = "Firmenname",
        CompanyAddress = "Firmenadresse",
        CompanyVatId = "UID",
        TseStatus = "TSE-Status",
        TseProvider = "TSE",
        DepExport = "DEP-Export",
        TotalSales = "Umsatz",
        TotalCash = "Bar",
        TotalCard = "Karte",
        TotalVoucher = "Gutschein",
        TotalOther = "Sonstige",
        PaymentMethodsSection = "Zahlungsarten",
        FinancialSummarySection = "Finanzübersicht",
        TransactionBreakdownSection = "Transaktionen (Anzahl)",
        RksvStatusSection = "RKSV / FinanzOnline",
        Startbeleg = "Startbeleg",
        Monatsbeleg = "Monatsbeleg",
        Jahresbeleg = "Jahresbeleg",
        BreakdownCash = "Bar",
        BreakdownCard = "Karte",
        BreakdownVoucher = "Gutschein",
        BreakdownCancellations = "Stornos",
        BreakdownTotal = "Gesamt",
        CashCount = "Gezähltes Bargeld",
        Difference = "Differenz",
        FiscalTotal = "Fiskal Brutto",
        FiscalNet = "Fiskal Netto",
        FiscalTax = "Fiskal MwSt.",
        TaxSection = "MwSt.-Aufschlüsselung",
        Tax20 = "20 % MwSt.",
        Tax10 = "10 % MwSt.",
        Tax0 = "0 % MwSt.",
        PreviousSignature = "Vorherige TSE-Signatur",
        Transactions = "Transaktionen",
        TseSignature = "TSE-Signatur",
        TseVerification = "Verifizierung",
        Disclaimer = DailyClosingReportComposer.RksvDailyDisclaimerDe,
    };

    public static DailyClosingReportLabels EnglishDailyReport { get; } = new()
    {
        Title = "Daily Closing Report",
        Date = "Date",
        Period = "Period",
        Register = "Register",
        CashRegisterId = "Register ID",
        Cashier = "Employee",
        ShiftNumber = "Shift no.",
        CompanyName = "Company name",
        CompanyAddress = "Company address",
        CompanyVatId = "VAT ID",
        TseStatus = "TSE status",
        TseProvider = "TSE",
        DepExport = "DEP export",
        TotalSales = "Sales",
        TotalCash = "Cash",
        TotalCard = "Card",
        TotalVoucher = "Voucher",
        TotalOther = "Other",
        PaymentMethodsSection = "Payment methods",
        FinancialSummarySection = "Financial summary",
        TransactionBreakdownSection = "Transactions (count)",
        RksvStatusSection = "RKSV / FinanzOnline",
        Startbeleg = "Start receipt",
        Monatsbeleg = "Monthly receipt",
        Jahresbeleg = "Annual receipt",
        BreakdownCash = "Cash",
        BreakdownCard = "Card",
        BreakdownVoucher = "Voucher",
        BreakdownCancellations = "Cancellations",
        BreakdownTotal = "Total",
        CashCount = "Cash count",
        Difference = "Difference",
        FiscalTotal = "Fiscal gross",
        FiscalNet = "Fiscal net",
        FiscalTax = "Fiscal tax",
        TaxSection = "VAT breakdown",
        Tax20 = "20% VAT",
        Tax10 = "10% VAT",
        Tax0 = "0% VAT",
        PreviousSignature = "Previous TSE signature",
        Transactions = "Transactions",
        TseSignature = "TSE signature",
        TseVerification = "Verification",
        Disclaimer = DailyClosingReportComposer.RksvDailyDisclaimerEn,
    };

    public static DailyClosingReportLabels TurkishDailyReport { get; } = new()
    {
        Title = "Günlük Kapanış Raporu",
        Date = "Tarih",
        Period = "Dönem",
        Register = "Kasa",
        CashRegisterId = "Kasa-ID",
        Cashier = "Çalışan",
        ShiftNumber = "Vardiya no.",
        CompanyName = "Firma adı",
        CompanyAddress = "Firma adresi",
        CompanyVatId = "UID",
        TseStatus = "TSE durumu",
        TseProvider = "TSE",
        DepExport = "DEP dışa aktarım",
        TotalSales = "Satış",
        TotalCash = "Nakit",
        TotalCard = "Kart",
        TotalVoucher = "Kupon",
        TotalOther = "Diğer",
        PaymentMethodsSection = "Ödeme türleri",
        FinancialSummarySection = "Finansal özet",
        TransactionBreakdownSection = "İşlemler (adet)",
        RksvStatusSection = "RKSV / FinanzOnline",
        Startbeleg = "Startbeleg",
        Monatsbeleg = "Monatsbeleg",
        Jahresbeleg = "Jahresbeleg",
        BreakdownCash = "Nakit",
        BreakdownCard = "Kart",
        BreakdownVoucher = "Kupon",
        BreakdownCancellations = "İptaller",
        BreakdownTotal = "Toplam",
        CashCount = "Sayılan nakit",
        Difference = "Fark",
        FiscalTotal = "Mali toplam",
        FiscalNet = "Mali net",
        FiscalTax = "Mali KDV",
        TaxSection = "KDV dökümü",
        Tax20 = "%20 KDV",
        Tax10 = "%10 KDV",
        Tax0 = "%0 KDV",
        PreviousSignature = "Önceki TSE imzası",
        Transactions = "İşlemler",
        TseSignature = "TSE imzası",
        TseVerification = "Doğrulama",
        Disclaimer = DailyClosingReportComposer.RksvDailyDisclaimerTr,
    };
}
