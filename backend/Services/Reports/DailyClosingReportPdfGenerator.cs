using System.Globalization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Time;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Reports;

public static class DailyClosingReportPdfGenerator
{
    /// <summary>Thermal roll width (~80 mm).</summary>
    private const float ThermalRollWidthPoints = 288f;

    private const float PageMarginPoints = 8f;
    private const float TseQrSizePoints = 96f;

    static DailyClosingReportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(
        PosDailyClosingReportDto report,
        DailyClosingReportLabels labels,
        CultureInfo culture,
        byte[]? tseSignatureQrPng = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(culture);

        var dateText = report.BusinessDate.ToString("dd.MM.yyyy", culture);
        var register = string.IsNullOrWhiteSpace(report.RegisterNumber) ? "—" : report.RegisterNumber.Trim();
        var cashRegisterId = report.CashRegisterId?.ToString("N") ?? register;
        var cashier = string.IsNullOrWhiteSpace(report.CashierName) ? "—" : report.CashierName.Trim();
        var shiftNumber = RksvShiftNumberFormatter.FormatOrDash(report.ShiftNumber);
        var companyName = string.IsNullOrWhiteSpace(report.CompanyName) ? "—" : report.CompanyName.Trim();
        var companyAddress = string.IsNullOrWhiteSpace(report.CompanyAddress) ? "—" : report.CompanyAddress.Trim();
        var companyVatId = string.IsNullOrWhiteSpace(report.CompanyVatId) ? "—" : report.CompanyVatId.Trim();
        var tseSignature = FormatTseSignature(report.TseSignature);
        var previousSignature = FormatTseSignature(report.PreviousClosingSignature);
        var disclaimer = culture.TwoLetterISOLanguageName == "de" &&
                         !string.IsNullOrWhiteSpace(report.SnapshotDisclaimerDe)
            ? report.SnapshotDisclaimerDe
            : labels.Disclaimer;
        var tseStatus = string.IsNullOrWhiteSpace(report.TseStatusLabel) ? "—" : report.TseStatusLabel.Trim();
        var tseProvider = string.IsNullOrWhiteSpace(report.TseProviderLabel) ? tseStatus : report.TseProviderLabel.Trim();
        var depExport = string.IsNullOrWhiteSpace(report.DepExportStatusLabel) ? "—" : report.DepExportStatusLabel.Trim();
        var tax = report.TaxBreakdown;

        var isDaily = string.Equals(report.ClosingType, "Daily", StringComparison.OrdinalIgnoreCase);

        var rows = new List<(string Label, string Value)>();

        rows.Add((labels.CompanyName, companyName));
        rows.Add((labels.CompanyAddress, companyAddress));
        rows.Add((labels.CompanyVatId, companyVatId));

        rows.AddRange(new (string Label, string Value)[]
        {
            (labels.Date, dateText),
            (labels.Period, FormatPeriod(report.PeriodStartUtc, report.PeriodEndUtc, culture)),
            (labels.CashRegisterId, cashRegisterId),
            (labels.Register, register),
            (labels.Cashier, cashier),
            (labels.ShiftNumber, shiftNumber),
            (labels.TseProvider, tseProvider),
            (labels.DepExport, depExport),
        });

        if (isDaily)
        {
            rows.Add((labels.FinancialSummarySection, string.Empty));
            rows.Add((labels.FiscalTotal, FormatMoney(report.FiscalTotalAmount, culture)));
            rows.Add((labels.FiscalNet, FormatMoney(report.FiscalTotalNetAmount, culture)));
            rows.Add((labels.FiscalTax, FormatMoney(report.FiscalTotalTaxAmount, culture)));
            rows.Add((labels.Transactions, report.FiscalTransactionCount.ToString(culture)));
        }
        else
        {
            rows.Add((labels.TotalSales, FormatMoney(report.TotalSales, culture)));
            rows.Add((labels.FiscalTotal, FormatMoney(report.FiscalTotalAmount, culture)));
            rows.Add((labels.FiscalTax, FormatMoney(report.FiscalTotalTaxAmount, culture)));
            rows.Add((labels.Transactions, report.FiscalTransactionCount.ToString(culture)));
        }

        if (isDaily && HasTaxBreakdown(tax))
        {
            rows.Add((labels.TaxSection, string.Empty));
            if (tax.GrossAt20 > 0m || tax.TaxAt20 > 0m)
                rows.Add((labels.Tax20, $"{FormatMoney(tax.GrossAt20, culture)} / {FormatMoney(tax.TaxAt20, culture)}"));
            if (tax.GrossAt10 > 0m || tax.TaxAt10 > 0m)
                rows.Add((labels.Tax10, $"{FormatMoney(tax.GrossAt10, culture)} / {FormatMoney(tax.TaxAt10, culture)}"));
            if (tax.GrossAt0 > 0m)
                rows.Add((labels.Tax0, FormatMoney(tax.GrossAt0, culture)));
        }

        if (isDaily)
        {
            rows.Add((labels.PaymentMethodsSection, string.Empty));
            rows.Add((labels.TotalCash, FormatMoney(report.TotalCash, culture)));
            rows.Add((labels.TotalCard, FormatMoney(report.TotalCard, culture)));
            rows.Add((labels.TotalVoucher, FormatMoney(report.TotalVoucherRedemptions, culture)));
            rows.Add((labels.TotalOther, FormatMoney(report.TotalOtherPaymentMethods, culture)));
            rows.Add((labels.TransactionBreakdownSection, string.Empty));
            rows.Add((labels.BreakdownCash, report.TransactionBreakdown.Cash.ToString(culture)));
            rows.Add((labels.BreakdownCard, report.TransactionBreakdown.Card.ToString(culture)));
            rows.Add((labels.BreakdownVoucher, report.TransactionBreakdown.Voucher.ToString(culture)));
            rows.Add((labels.BreakdownCancellations, report.TransactionBreakdown.Cancellations.ToString(culture)));
            rows.Add((labels.BreakdownTotal, report.TransactionBreakdown.Total.ToString(culture)));
            rows.Add((labels.CashCount, FormatMoney(report.CashCount, culture)));
            rows.Add((labels.Difference, FormatMoney(report.Difference, culture)));
            rows.Add((labels.RksvStatusSection, string.Empty));
            rows.Add((labels.Startbeleg, FormatBelegStatus(report.HasStartbeleg, culture)));
            rows.Add((labels.Monatsbeleg, FormatBelegStatus(report.HasMonatsbeleg, culture)));
            rows.Add((labels.Jahresbeleg, FormatBelegStatus(report.HasJahresbeleg, culture)));
        }

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(ThermalRollWidthPoints);
                page.Margin(PageMarginPoints);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().AlignCenter().Text(labels.Title).Bold().FontSize(12);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });

                        foreach (var (label, value) in rows)
                        {
                            if (string.IsNullOrEmpty(value))
                            {
                                table.Cell().ColumnSpan(2).PaddingVertical(4).Text(label).Bold().FontSize(8);
                                continue;
                            }

                            table.Cell().PaddingVertical(2).Text(label).FontColor(Colors.Grey.Darken1);
                            table.Cell().PaddingVertical(2).AlignRight().Text(value).SemiBold();
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(report.BackdatedNotice))
                    {
                        col.Item().PaddingTop(4).Text(report.BackdatedNotice)
                            .FontSize(7)
                            .FontColor(Colors.Orange.Darken2)
                            .LineHeight(1.3f);
                    }

                    if (!string.IsNullOrWhiteSpace(report.SalesFiscalReconciliationNote))
                    {
                        col.Item().PaddingTop(4).Text(report.SalesFiscalReconciliationNote)
                            .FontSize(7).FontColor(Colors.Orange.Darken2);
                    }

                    if (!string.IsNullOrWhiteSpace(report.DifferenceScopeNote))
                    {
                        col.Item().Text(report.DifferenceScopeNote)
                            .FontSize(7).FontColor(Colors.Grey.Darken1);
                    }

                    col.Item().PaddingTop(6).Text(labels.TseSignature).Bold().FontSize(8);
                    if (tseSignatureQrPng is { Length: > 0 })
                    {
                        col.Item().AlignCenter().Width(TseQrSizePoints).Height(TseQrSizePoints)
                            .Image(tseSignatureQrPng);
                    }

                    col.Item().Text(tseSignature).FontSize(6).WrapAnywhere();

                    if (isDaily)
                    {
                        col.Item().PaddingTop(4).Text($"{labels.TseVerification}: {FormatVerificationStatus(report, culture)}")
                            .FontSize(7)
                            .SemiBold()
                            .FontColor(report.TseSignatureVerified ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                    }

                    if (!string.IsNullOrWhiteSpace(report.PreviousClosingSignature))
                    {
                        col.Item().PaddingTop(4).Text(labels.PreviousSignature).Bold().FontSize(7);
                        col.Item().Text(previousSignature).FontSize(6).WrapAnywhere();
                    }

                    if (!string.IsNullOrWhiteSpace(report.TseStatusBadge))
                    {
                        col.Item().PaddingTop(4).AlignCenter().Text(report.TseStatusBadge)
                            .Bold()
                            .FontSize(9)
                            .FontColor(report.IsDemoFiscal ? Colors.Orange.Darken2 : Colors.Green.Darken2);
                    }

                    col.Item().PaddingTop(8).Text(disclaimer)
                        .FontSize(7)
                        .FontColor(Colors.Grey.Darken1)
                        .LineHeight(1.35f);
                });
            });
        }).GeneratePdf();
    }

    private static string FormatMoney(decimal amount, CultureInfo culture) =>
        amount.ToString("C", culture);

    private static string FormatTseSignature(string? signature) =>
        string.IsNullOrWhiteSpace(signature) ? "—" : signature.Trim();

    private static bool HasTaxBreakdown(Models.Reports.DailyClosingTaxBreakdownDto tax) =>
        tax.GrossAt20 > 0m || tax.TaxAt20 > 0m
        || tax.GrossAt10 > 0m || tax.TaxAt10 > 0m
        || tax.GrossAt0 > 0m;

    private static string FormatBelegStatus(bool exists, CultureInfo culture) =>
        culture.TwoLetterISOLanguageName switch
        {
            "en" => exists ? "Yes" : "No",
            "tr" => exists ? "Evet" : "Hayır",
            _ => exists ? "Ja" : "Nein",
        };

    private static string FormatVerificationStatus(PosDailyClosingReportDto report, CultureInfo culture)
    {
        if (report.TseSignatureVerified)
            return culture.TwoLetterISOLanguageName switch
            {
                "en" => "VERIFIED",
                "tr" => "DOĞRULANDI",
                _ => "GEPRÜFT",
            };

        if (report.IsDemoFiscal)
            return culture.TwoLetterISOLanguageName switch
            {
                "en" => "SIMULATED",
                "tr" => "SİMÜLE",
                _ => "SIMULIERT",
            };

        return culture.TwoLetterISOLanguageName switch
        {
            "en" => "NOT VERIFIED",
            "tr" => "DOĞRULANMADI",
            _ => "NICHT VERIFIZIERT",
        };
    }

    private static string FormatPeriod(DateTime? periodStartUtc, DateTime? periodEndUtc, CultureInfo culture)
    {
        if (!periodStartUtc.HasValue || !periodEndUtc.HasValue)
            return "—";

        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(
            periodStartUtc.Value,
            PostgreSqlUtcDateTime.AustriaTimeZone);
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(
            periodEndUtc.Value,
            PostgreSqlUtcDateTime.AustriaTimeZone);

        return $"{startLocal.ToString("dd.MM.yyyy HH:mm", culture)} – {endLocal.ToString("dd.MM.yyyy HH:mm", culture)}";
    }
}
