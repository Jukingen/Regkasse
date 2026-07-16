using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Maps fiscal closing + payment snapshot into a consistent RKSV report DTO.</summary>
public static class DailyClosingReportComposer
{
    public const string RksvDailyDisclaimerDe =
        "RKSV-konformer Tagesabschluss\n" +
        "Registrierkassensicherheitsverordnung (RKSV)\n" +
        "Dieser Tagesabschluss ist fiskalisch gültig.";

    public const string RksvDailyDisclaimerEn =
        "RKSV-compliant daily closing\n" +
        "Cash Register Security Ordinance (RKSV)\n" +
        "This daily closing is fiscally valid.";

    public const string RksvDailyDisclaimerTr =
        "RKSV uyumlu günlük kapanış\n" +
        "Kasa Güvenlik Yönetmeliği (RKSV)\n" +
        "Bu günlük kapanış mali olarak geçerlidir.";

    public const string RksvDailyDemoDisclaimerDe =
        "DEMO / NICHT FISKAL\n" +
        "Kein fiskal gültiger Tagesabschluss — nur zu Testzwecken.\n" +
        "TSE-Signatur simuliert.";

    public const string RksvDailyDemoDisclaimerEn =
        "DEMO / NOT FISCAL\n" +
        "Not a fiscally valid daily closing — for testing only.\n" +
        "TSE signature is simulated.";

    public const string RksvDailyDemoDisclaimerTr =
        "DEMO / MALİ DEĞİL\n" +
        "Mali olarak geçerli günlük kapanış değil — yalnızca test amaçlı.\n" +
        "TSE imzası simüle edilmiştir.";

    public const string RksvPeriodDisclaimerDe =
        "RKSV-konformer Periodenabschluss mit TSE-Signatur — für die Betriebsprüfung aufbewahren.";

    private const decimal AmountTolerance = 0.005m;

    public static PosDailyClosingReportDto Compose(
        DailyClosing closing,
        string? registerNumber,
        DailyClosingSummaryDto? daySummary,
        decimal cashCount,
        decimal shiftDifference,
        decimal? shiftCashSales = null,
        string? cashierName = null,
        string? previousClosingSignature = null,
        string? shiftNumber = null,
        FiscalEnvironmentResolver.FiscalEnvironment? fiscalEnvironment = null,
        TagesabschlussCloudContext? cloudContext = null)
    {
        ArgumentNullException.ThrowIfNull(closing);

        var fiscalEnv = fiscalEnvironment
                        ?? new FiscalEnvironmentResolver.FiscalEnvironment(
                            false,
                            "Production",
                            "RKSV-konform (Registrierkassensicherheitsverordnung)",
                            "TSE: AKTIV ✅",
                            "TSE AKTIV");

        var isDaily = string.Equals(closing.ClosingType, "Daily", StringComparison.OrdinalIgnoreCase);
        var fiscalTotal = closing.TotalAmount;

        decimal totalSales;
        decimal totalCash;
        decimal totalCard;
        decimal totalVoucher;
        decimal totalOther;
        PaymentBreakdown paymentBreakdown = new();
        TransactionBreakdown transactionBreakdown = new();
        DailyClosingTaxBreakdownDto taxBreakdown = new();
        string? reconciliationNote = null;
        string? differenceNote = null;

        if (isDaily && daySummary != null)
        {
            totalSales = daySummary.TotalSales;
            totalCash = daySummary.TotalCash;
            totalCard = daySummary.TotalCard;
            totalVoucher = daySummary.TotalVoucherRedemptions;
            totalOther = daySummary.TotalOtherPaymentMethods;
            paymentBreakdown = daySummary.PaymentBreakdown;
            transactionBreakdown = daySummary.TransactionBreakdown;
            taxBreakdown = daySummary.TaxBreakdown;

            if (Math.Abs(totalSales - fiscalTotal) > AmountTolerance)
            {
                reconciliationNote =
                    $"Zahlungszeilen {totalSales:F2} € vs. fiskal signiert {fiscalTotal:F2} € — Abweichung prüfen (Stornos/Sonderbelege).";
            }
            else
            {
                totalSales = fiscalTotal;
            }

            if (shiftCashSales.HasValue && Math.Abs(shiftCashSales.Value - totalCash) > AmountTolerance)
            {
                differenceNote =
                    $"Differenz bezieht sich auf die Schicht (Bar {shiftCashSales.Value:F2} €); Tages-Barumsatz: {totalCash:F2} €.";
            }
        }
        else
        {
            totalSales = fiscalTotal;
            totalCash = 0m;
            totalCard = 0m;
            totalVoucher = 0m;
            totalOther = 0m;
            paymentBreakdown = fiscalEnv.IsDemoFiscal
                ? PaymentBreakdown.CreateDemo()
                : PaymentBreakdown.FromAmounts(0m, 0m, 0m, 0m);
            transactionBreakdown = new TransactionBreakdown
            {
                Total = closing.TransactionCount,
            };
        }

        if (paymentBreakdown.Total == 0m && (totalCash + totalCard + totalVoucher + totalOther) > 0m)
        {
            paymentBreakdown = PaymentBreakdown.FromAmounts(totalCash, totalCard, totalVoucher, totalOther);
        }

        var disclaimer = isDaily
            ? TagesabschlussReportService.FormatFooter(fiscalEnv.IsDemoFiscal)
            : RksvPeriodDisclaimerDe;
        var backdatedNotice = isDaily ? DailyClosingBackdatedReportNote.TryFormat(closing) : null;
        var qrPayload = FiscalEnvironmentResolver.BuildClosingQrPayload(
            fiscalEnv.IsDemoFiscal,
            closing.TseSignature,
            closing.ClosingDate,
            fiscalTotal);

        return new PosDailyClosingReportDto
        {
            ClosingType = closing.ClosingType,
            BusinessDate = closing.ClosingDate,
            CashRegisterId = closing.CashRegisterId,
            RegisterNumber = cloudContext?.RegisterNumber ?? registerNumber,
            CompanyName = cloudContext?.CompanyName,
            CompanyAddress = cloudContext?.CompanyAddress,
            CompanyVatId = cloudContext?.CompanyVatId,
            PeriodStartUtc = cloudContext?.PeriodStartUtc,
            PeriodEndUtc = cloudContext?.PeriodEndUtc,
            TseProviderLabel = cloudContext?.TseProviderLabel,
            DepExportStatusLabel = cloudContext?.DepExportStatusLabel,
            TseSignatureVerified = cloudContext?.TseSignatureVerified ?? false,
            HasStartbeleg = cloudContext?.HasStartbeleg ?? false,
            HasMonatsbeleg = cloudContext?.HasMonatsbeleg ?? false,
            HasJahresbeleg = cloudContext?.HasJahresbeleg ?? false,
            CashierName = cashierName,
            ShiftNumber = shiftNumber,
            TotalSales = totalSales,
            TotalCash = totalCash,
            TotalCard = totalCard,
            TotalVoucherRedemptions = totalVoucher,
            TotalOtherPaymentMethods = totalOther,
            CashCount = cashCount,
            Difference = shiftDifference,
            FiscalTotalAmount = fiscalTotal,
            FiscalTotalTaxAmount = closing.TotalTaxAmount,
            FiscalTotalNetAmount = fiscalTotal - closing.TotalTaxAmount,
            FiscalTransactionCount = closing.TransactionCount,
            TseSignature = closing.TseSignature,
            PreviousClosingSignature = previousClosingSignature,
            TaxBreakdown = taxBreakdown,
            PaymentBreakdown = paymentBreakdown,
            IsDemoFiscal = fiscalEnv.IsDemoFiscal,
            FiscalEnvironment = fiscalEnv.EnvironmentName,
            TseStatusLabel = fiscalEnv.TseStatusDisplay,
            TseStatusBadge = fiscalEnv.TseStatusBadge,
            RksvFooterLabel = fiscalEnv.RksvFooterLabel,
            QrPayload = qrPayload,
            SnapshotDisclaimerDe = disclaimer,
            BackdatedNotice = backdatedNotice,
            SalesFiscalReconciliationNote = reconciliationNote,
            DifferenceScopeNote = differenceNote,
            TransactionBreakdown = transactionBreakdown,
        };
    }
}
