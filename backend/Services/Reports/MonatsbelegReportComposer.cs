using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Maps persisted <see cref="Monatsbeleg"/> + summary into RKSV monthly report DTO.</summary>
public static class MonatsbelegReportComposer
{
    public const string RksvMonthlyDisclaimerDe =
        "RKSV-konformer Monatsabschluss\n" +
        "Registrierkassensicherheitsverordnung (RKSV)\n" +
        "Dieser Monatsabschluss ist fiskalisch gültig.";

    public const string RksvMonthlyDemoDisclaimerDe =
        "DEMO / NICHT FISKAL\n" +
        "Kein fiskal gültiger Monatsabschluss — nur zu Testzwecken.\n" +
        "TSE-Signatur simuliert.";

    public static PosDailyClosingReportDto Compose(
        Monatsbeleg closing,
        MonatsbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment)
    {
        ArgumentNullException.ThrowIfNull(closing);
        ArgumentNullException.ThrowIfNull(summary);

        var monthAnchor = new DateTime(closing.Year, closing.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var disclaimer = fiscalEnvironment.IsDemoFiscal
            ? TagesabschlussReportService.FormatFooter(true)
            : RksvMonthlyDisclaimerDe;

        var qrPayload = FiscalEnvironmentResolver.BuildClosingQrPayload(
            fiscalEnvironment.IsDemoFiscal,
            closing.TseSignature,
            monthAnchor,
            closing.TotalGross);

        return new PosDailyClosingReportDto
        {
            ClosingType = "Monthly",
            BusinessDate = monthAnchor,
            RegisterNumber = registerNumber,
            TotalSales = closing.TotalGross,
            TotalCash = closing.TotalCash,
            TotalCard = closing.TotalCard,
            TotalVoucherRedemptions = closing.TotalVoucher,
            TotalOtherPaymentMethods = closing.TotalOther,
            FiscalTotalAmount = closing.TotalGross,
            FiscalTotalTaxAmount = closing.TotalTax,
            FiscalTransactionCount = closing.TransactionCount,
            TseSignature = closing.TseSignature,
            PreviousClosingSignature = closing.PreviousSignature,
            TaxBreakdown = summary.TaxBreakdown,
            PaymentBreakdown = summary.PaymentBreakdown,
            TransactionBreakdown = summary.TransactionBreakdown,
            IsDemoFiscal = fiscalEnvironment.IsDemoFiscal,
            FiscalEnvironment = fiscalEnvironment.EnvironmentName,
            TseStatusLabel = fiscalEnvironment.TseStatusDisplay,
            TseStatusBadge = fiscalEnvironment.TseStatusBadge,
            RksvFooterLabel = fiscalEnvironment.RksvFooterLabel,
            QrPayload = qrPayload,
            SnapshotDisclaimerDe = disclaimer,
        };
    }

    public static MonatsbelegDetailDto ToDetailDto(
        Monatsbeleg entity,
        MonatsbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment) =>
        new()
        {
            Id = entity.Id,
            CashRegisterId = entity.CashRegisterId,
            RegisterNumber = registerNumber,
            Year = entity.Year,
            Month = entity.Month,
            Summary = summary,
            TseSignature = entity.TseSignature,
            PreviousSignature = entity.PreviousSignature,
            SignatureChainLength = entity.SignatureChainLength,
            IsSimulated = entity.IsSimulated,
            Environment = entity.Environment,
            RksvFooterLabel = fiscalEnvironment.RksvFooterLabel,
            TseStatusBadge = fiscalEnvironment.TseStatusBadge,
            CreatedAtUtc = entity.CreatedAtUtc,
            DailyClosingId = entity.DailyClosingId,
        };
}
