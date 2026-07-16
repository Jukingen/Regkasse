using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Maps RKSV domain rows/DTOs into the unified <see cref="RksvReportTemplate"/>.</summary>
public static class RksvReportTemplateMapper
{
    public static RksvReportTemplate FromTagesabschluss(
        TagesabschlussReportModel model,
        string environmentDisplay,
        string rksvFooter,
        string qrPayload,
        string? registerNumber = null) =>
        new()
        {
            CompanyName = model.CompanyName,
            CompanyAddress = model.CompanyAddress,
            CompanyVatId = model.CompanyVatId,
            ReportName = RksvReportNames.Tagesabschluss,
            CashRegisterId = model.CashRegisterId.ToString("N"),
            RegisterNumber = registerNumber,
            PeriodStart = model.PeriodStart,
            PeriodEnd = model.PeriodEnd,
            DocumentDate = model.ClosingDate,
            TotalGross = model.TotalGross,
            TotalNet = model.TotalNet,
            TaxAmount = model.TotalGross - model.TotalNet,
            TransactionCount = model.TransactionCount,
            CashierName = model.CashierName,
            ShiftNumber = model.ShiftNumber,
            TseSignature = model.TseSignature,
            TseSignatureTimestamp = model.TseSignatureTimestamp,
            TseProvider = model.TseProviderLabel,
            IsSimulated = model.IsSimulated,
            TseSignatureVerified = model.TseSignatureVerified,
            HasStartbeleg = model.HasStartbeleg,
            HasMonatsbeleg = model.HasMonatsbeleg,
            HasJahresbeleg = model.HasJahresbeleg,
            DepExportStatus = model.DepExportStatusLabel,
            QrCode = qrPayload,
            RksvFooter = rksvFooter,
            OperatorNotice = model.IsBackdated
                ? DailyClosingBackdatedReportNote.Format(
                    model.ClosingDate,
                    model.CreatedAt,
                    model.LateCreationReason)
                : null,
            EnvironmentDisplay = environmentDisplay,
            CashTotal = model.CashTotal,
            CardTotal = model.CardTotal,
            VoucherTotal = model.VoucherTotal,
            TaxRate20 = model.TaxRate20,
            TaxRate10 = model.TaxRate10,
            TaxRate0 = model.TaxRate0,
            GeneratedAt = DateTime.UtcNow,
        };

    public static RksvReportTemplate FromClosingReport(
        PosDailyClosingReportDto report,
        TagesabschlussCloudContext? cloudContext = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var reportName = report.ClosingType?.Trim() switch
        {
            "Monthly" => RksvReportNames.Monatsbeleg,
            "Yearly" => RksvReportNames.Jahresbeleg,
            _ => RksvReportNames.Tagesabschluss,
        };

        var tax = report.TaxBreakdown;
        var payments = report.PaymentBreakdown;

        return new RksvReportTemplate
        {
            CompanyName = ResolveCompanyName(report.CompanyName, cloudContext?.CompanyName),
            CompanyAddress = ResolveCompanyField(report.CompanyAddress, cloudContext?.CompanyAddress),
            CompanyVatId = ResolveCompanyField(report.CompanyVatId, cloudContext?.CompanyVatId),
            ReportName = reportName,
            CashRegisterId = (report.CashRegisterId ?? Guid.Empty).ToString("N"),
            RegisterNumber = cloudContext?.RegisterNumber ?? report.RegisterNumber,
            PeriodStart = cloudContext?.PeriodStartUtc ?? report.PeriodStartUtc,
            PeriodEnd = cloudContext?.PeriodEndUtc ?? report.PeriodEndUtc,
            DocumentDate = report.BusinessDate,
            TotalGross = report.FiscalTotalAmount,
            TotalNet = report.FiscalTotalNetAmount > 0m
                ? report.FiscalTotalNetAmount
                : report.FiscalTotalAmount - report.FiscalTotalTaxAmount,
            TaxAmount = report.FiscalTotalTaxAmount,
            TransactionCount = report.FiscalTransactionCount,
            CashierName = report.CashierName,
            ShiftNumber = report.ShiftNumber,
            TseSignature = report.TseSignature,
            TseProvider = cloudContext?.TseProviderLabel ?? report.TseProviderLabel ?? "fiskaly Cloud-HSM",
            IsSimulated = report.IsDemoFiscal,
            TseSignatureVerified = cloudContext?.TseSignatureVerified ?? report.TseSignatureVerified,
            HasStartbeleg = cloudContext?.HasStartbeleg ?? report.HasStartbeleg,
            HasMonatsbeleg = cloudContext?.HasMonatsbeleg ?? report.HasMonatsbeleg,
            HasJahresbeleg = cloudContext?.HasJahresbeleg ?? report.HasJahresbeleg,
            DepExportStatus = cloudContext?.DepExportStatusLabel ?? report.DepExportStatusLabel,
            QrCode = report.QrPayload ?? string.Empty,
            RksvFooter = report.SnapshotDisclaimerDe,
            OperatorNotice = report.BackdatedNotice,
            EnvironmentDisplay = report.FiscalEnvironment,
            CashTotal = payments.Cash > 0m ? payments.Cash : report.TotalCash,
            CardTotal = payments.Card > 0m ? payments.Card : report.TotalCard,
            VoucherTotal = payments.Voucher > 0m ? payments.Voucher : report.TotalVoucherRedemptions,
            TaxRate20 = tax.TaxAt20,
            TaxRate10 = tax.TaxAt10,
            TaxRate0 = tax.GrossAt0,
            GeneratedAt = DateTime.UtcNow,
        };
    }

    public static RksvReportTemplate FromReceipt(
        ReceiptDTO receipt,
        TagesabschlussCloudContext? cloudContext = null,
        string? rksvFooter = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var reportName = ResolveReceiptReportName(receipt);
        var isSimulated = receipt.RksvFooterLabel.Contains("DEMO", StringComparison.OrdinalIgnoreCase);
        var tax20 = receipt.TaxRates.FirstOrDefault(t => t.Rate >= 20m)?.TaxAmount;
        var tax10 = receipt.TaxRates.FirstOrDefault(t => t is { Rate: >= 10m and < 20m })?.TaxAmount;
        var tax0 = receipt.TaxRates.FirstOrDefault(t => t.Rate < 10m)?.GrossAmount;

        return new RksvReportTemplate
        {
            CompanyName = ResolveCompanyName(receipt.Company.Name, cloudContext?.CompanyName),
            CompanyAddress = ResolveCompanyField(receipt.Company.Address, cloudContext?.CompanyAddress),
            CompanyVatId = ResolveCompanyField(receipt.Company.TaxNumber, cloudContext?.CompanyVatId),
            ReportName = reportName,
            CashRegisterId = receipt.CashRegisterId.ToString("N"),
            RegisterNumber = cloudContext?.RegisterNumber ?? receipt.DisplayRegisterNumber,
            DocumentDate = receipt.Date,
            PeriodStart = cloudContext?.PeriodStartUtc,
            PeriodEnd = cloudContext?.PeriodEndUtc,
            TotalGross = receipt.GrandTotal,
            TotalNet = receipt.SubTotal,
            TaxAmount = receipt.TaxAmount,
            TransactionCount = 1,
            CashierName = receipt.CashierDisplayName ?? receipt.CashierId,
            ShiftNumber = receipt.ShiftNumber ?? RksvShiftNumberFormatter.Format(receipt.ShiftId),
            TseSignature = receipt.Signature?.SignatureValue,
            TseSignatureTimestamp = receipt.Signature?.Timestamp,
            TseProvider = cloudContext?.TseProviderLabel
                          ?? (isSimulated ? "TSE simuliert (Demo)" : "fiskaly Cloud-HSM"),
            IsSimulated = isSimulated,
            TseSignatureVerified = cloudContext?.TseSignatureVerified
                                   ?? (!isSimulated && !string.IsNullOrWhiteSpace(receipt.Signature?.SignatureValue)),
            HasStartbeleg = cloudContext?.HasStartbeleg ?? false,
            HasMonatsbeleg = cloudContext?.HasMonatsbeleg ?? false,
            HasJahresbeleg = cloudContext?.HasJahresbeleg ?? false,
            DepExportStatus = cloudContext?.DepExportStatusLabel,
            FinanzOnlineStatus = FormatFinanzOnlineStatus(receipt.RksvFinanzOnlineSubmission),
            QrCode = receipt.Signature?.QrData ?? string.Empty,
            RksvFooter = rksvFooter ?? receipt.RksvFooterLabel,
            ReceiptNumber = receipt.ReceiptNumber,
            TaxRate20 = tax20,
            TaxRate10 = tax10,
            TaxRate0 = tax0,
            LineItems = receipt.Items
                .Select(i => new RksvReportLineItem
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    UnitPriceGross = i.UnitPrice,
                    LineTotalGross = i.TotalPrice,
                })
                .ToList(),
            GeneratedAt = receipt.ReceiptPersistedAtUtc == default
                ? DateTime.UtcNow
                : receipt.ReceiptPersistedAtUtc,
        };
    }

    public static string ResolveReceiptReportName(ReceiptDTO receipt)
    {
        if (string.Equals(receipt.FiscalTraceKind, "Storno", StringComparison.OrdinalIgnoreCase))
            return RksvReportNames.StornoBeleg;

        if (string.Equals(receipt.FiscalTraceKind, "Refund", StringComparison.OrdinalIgnoreCase))
            return RksvReportNames.Erstattungsbeleg;

        return receipt.RksvSpecialReceiptKind?.Trim() switch
        {
            RksvSpecialReceiptKinds.Nullbeleg => RksvReportNames.Nullbeleg,
            RksvSpecialReceiptKinds.Startbeleg => RksvReportNames.Startbeleg,
            RksvSpecialReceiptKinds.Monatsbeleg => RksvReportNames.Monatsbeleg,
            RksvSpecialReceiptKinds.Jahresbeleg => RksvReportNames.Jahresbeleg,
            RksvSpecialReceiptKinds.Schlussbeleg => RksvReportNames.Schlussbeleg,
            null or "" => RksvReportNames.Beleg,
            _ => receipt.RksvSpecialReceiptKind!,
        };
    }

    private static string? FormatFinanzOnlineStatus(RksvFinanzOnlineSubmissionStatusDto? submission)
    {
        if (submission == null)
            return null;

        if (!string.IsNullOrWhiteSpace(submission.Status))
            return submission.Status.Trim();

        return submission.SubmittedAtUtc.HasValue ? "Übermittelt" : "Ausstehend";
    }

    private static string ResolveCompanyName(string? primary, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary)
            ? primary.Trim()
            : string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim();

    private static string ResolveCompanyField(string? primary, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary)
            ? primary.Trim()
            : string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim();
}
