using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Maps persisted <see cref="Jahresbeleg"/> + summary into RKSV yearly report DTO.</summary>
public static class JahresbelegReportComposer
{
    public const string RksvYearlyDisclaimerDe =
        "RKSV-konformer Jahresabschluss\n" +
        "Registrierkassensicherheitsverordnung (RKSV)\n" +
        "Dieser Jahresabschluss ist fiskalisch gültig.";

    public const string RksvYearlyDemoDisclaimerDe =
        "DEMO / NICHT FISKAL\n" +
        "Kein fiskal gültiger Jahresabschluss — nur zu Testzwecken.\n" +
        "TSE-Signatur simuliert.";

    public static PosDailyClosingReportDto Compose(
        Jahresbeleg closing,
        JahresbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment,
        TagesabschlussCloudContext? cloudContext = null,
        DailyClosing? linkedDailyClosing = null)
    {
        ArgumentNullException.ThrowIfNull(closing);
        ArgumentNullException.ThrowIfNull(summary);

        var yearAnchor = new DateTime(closing.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var disclaimer = fiscalEnvironment.IsDemoFiscal
            ? TagesabschlussReportService.FormatFooter(true)
            : RksvYearlyDisclaimerDe;

        var qrPayload = FiscalEnvironmentResolver.BuildClosingQrPayload(
            fiscalEnvironment.IsDemoFiscal,
            closing.TseSignature,
            yearAnchor,
            closing.TotalGross);

        var (periodStartUtc, periodEndUtc) = RksvClosingPeriodHelper.YearUtcRange(closing.Year);

        return new PosDailyClosingReportDto
        {
            ClosingType = "Yearly",
            BusinessDate = yearAnchor,
            CashRegisterId = closing.CashRegisterId,
            RegisterNumber = cloudContext?.RegisterNumber ?? registerNumber,
            CompanyName = cloudContext?.CompanyName,
            CompanyAddress = cloudContext?.CompanyAddress,
            CompanyVatId = cloudContext?.CompanyVatId,
            PeriodStartUtc = cloudContext?.PeriodStartUtc ?? periodStartUtc,
            PeriodEndUtc = cloudContext?.PeriodEndUtc ?? periodEndUtc,
            TseProviderLabel = cloudContext?.TseProviderLabel,
            DepExportStatusLabel = cloudContext?.DepExportStatusLabel,
            TseSignatureVerified = cloudContext?.TseSignatureVerified ?? false,
            HasStartbeleg = cloudContext?.HasStartbeleg ?? false,
            HasMonatsbeleg = cloudContext?.HasMonatsbeleg ?? false,
            HasJahresbeleg = cloudContext?.HasJahresbeleg ?? true,
            CashierName = string.IsNullOrWhiteSpace(linkedDailyClosing?.CashierName)
                ? null
                : linkedDailyClosing!.CashierName,
            ShiftNumber = RksvShiftNumberFormatter.Format(linkedDailyClosing?.ShiftNumber),
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

    public static JahresbelegDetailDto ToDetailDto(
        Jahresbeleg entity,
        JahresbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment) =>
        new()
        {
            Id = entity.Id,
            CashRegisterId = entity.CashRegisterId,
            RegisterNumber = registerNumber,
            Year = entity.Year,
            Summary = summary,
            TseSignature = entity.TseSignature,
            PreviousSignature = entity.PreviousSignature,
            SignatureChainLength = entity.SignatureChainLength,
            IsSimulated = entity.IsSimulated,
            Environment = entity.Environment,
            IsDecemberMonatsbeleg = entity.IsDecemberMonatsbeleg,
            RksvFooterLabel = fiscalEnvironment.RksvFooterLabel,
            TseStatusBadge = fiscalEnvironment.TseStatusBadge,
            CreatedAtUtc = entity.CreatedAtUtc,
            DailyClosingId = entity.DailyClosingId,
        };

    public static JahresbelegSummaryDto BuildSummaryFromEntity(Jahresbeleg entity)
    {
        var references = JahresbelegYearlyAggregator.DeserializeMonthlyReferences(entity.MonthlyReferences);
        return new JahresbelegSummaryDto
        {
            Year = entity.Year,
            CashRegisterId = entity.CashRegisterId,
            MonatsbelegCount = references.Count,
            MonthlyReferences = references,
            TotalCash = entity.TotalCash,
            TotalCard = entity.TotalCard,
            TotalVoucher = entity.TotalVoucher,
            TotalOther = entity.TotalOther,
            TotalGross = entity.TotalGross,
            TotalTax = entity.TotalTax,
            TaxRate20 = entity.TaxRate20,
            TaxRate10 = entity.TaxRate10,
            TaxRate0 = entity.TaxRate0,
            TransactionCount = entity.TransactionCount,
            PaymentBreakdown = PaymentBreakdown.FromAmounts(
                entity.TotalCash,
                entity.TotalCard,
                entity.TotalVoucher,
                entity.TotalOther),
            TaxBreakdown = new DailyClosingTaxBreakdownDto
            {
                TaxAt20 = entity.TaxRate20,
                TaxAt10 = entity.TaxRate10,
                GrossAt0 = entity.TaxRate0,
            },
            IsDecemberMonatsbeleg = entity.IsDecemberMonatsbeleg,
        };
    }
}
