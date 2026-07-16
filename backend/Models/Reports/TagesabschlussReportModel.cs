using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.Reports;

/// <summary>
/// Cloud POS Tagesabschluss report projection (RKSV). Assembled at read/print time from
/// <see cref="DailyClosing"/> plus tenant company settings — not persisted as duplicate columns.
/// On-premise EFR/hardware fields (Smartcard, Stationskennung, etc.) are intentionally omitted.
/// </summary>
public sealed class TagesabschlussReportModel
{
    public Guid Id { get; init; }

    public Guid CashRegisterId { get; init; }

    public DateTime ClosingDate { get; init; }

    /// <summary>Real UTC creation time (never forged for late closings).</summary>
    public DateTime CreatedAt { get; init; }

    public bool IsBackdated { get; init; }

    public string? LateCreationReason { get; init; }

    public DateTime? PeriodStart { get; init; }

    public DateTime? PeriodEnd { get; init; }

    public string CompanyName { get; init; } = string.Empty;

    public string CompanyAddress { get; init; } = string.Empty;

    public string CompanyVatId { get; init; } = string.Empty;

    public decimal TotalGross { get; init; }

    public decimal TotalNet { get; init; }

    public decimal TaxRate20 { get; init; }

    public decimal TaxRate10 { get; init; }

    public decimal TaxRate0 { get; init; }

    public decimal CashTotal { get; init; }

    public decimal CardTotal { get; init; }

    public decimal VoucherTotal { get; init; }

    public string? TseSignature { get; init; }

    public string? TseSignatureTimestamp { get; init; }

    public bool IsSimulated { get; init; }

    public int TransactionCount { get; init; }

    public string? CashierName { get; init; }

    public string? ShiftNumber { get; init; }

    public bool HasStartbeleg { get; init; }

    public bool HasMonatsbeleg { get; init; }

    public bool HasJahresbeleg { get; init; }

    public string TseProviderLabel { get; init; } = string.Empty;

    public string DepExportStatusLabel { get; init; } = string.Empty;

    public bool TseSignatureVerified { get; init; }

    public static TagesabschlussReportModel From(
        DailyClosing closing,
        TagesabschlussCloudContext cloud,
        DailyClosingSummaryDto? daySummary = null,
        string? cashierName = null,
        string? shiftNumber = null)
    {
        ArgumentNullException.ThrowIfNull(closing);
        ArgumentNullException.ThrowIfNull(cloud);

        var payments = daySummary?.PaymentBreakdown
                       ?? PaymentBreakdown.FromAmounts(
                           daySummary?.TotalCash ?? 0m,
                           daySummary?.TotalCard ?? 0m,
                           daySummary?.TotalVoucherRedemptions ?? 0m,
                           daySummary?.TotalOtherPaymentMethods ?? 0m);

        var tax = daySummary?.TaxBreakdown ?? new DailyClosingTaxBreakdownDto();
        var totalGross = closing.TotalAmount;
        var totalTax = closing.TotalTaxAmount > 0m
            ? closing.TotalTaxAmount
            : daySummary?.FiscalTotalTaxAmount ?? 0m;

        return new TagesabschlussReportModel
        {
            Id = closing.Id,
            CashRegisterId = closing.CashRegisterId,
            ClosingDate = closing.ClosingDate,
            CreatedAt = closing.CreatedAt,
            IsBackdated = closing.IsBackdated,
            LateCreationReason = closing.LateCreationReason,
            PeriodStart = cloud.PeriodStartUtc,
            PeriodEnd = cloud.PeriodEndUtc,
            CompanyName = cloud.CompanyName,
            CompanyAddress = cloud.CompanyAddress,
            CompanyVatId = cloud.CompanyVatId,
            TotalGross = totalGross,
            TotalNet = totalGross - totalTax,
            TaxRate20 = tax.TaxAt20,
            TaxRate10 = tax.TaxAt10,
            TaxRate0 = tax.GrossAt0,
            CashTotal = payments.Cash,
            CardTotal = payments.Card,
            VoucherTotal = payments.Voucher,
            TseSignature = closing.TseSignature,
            TseSignatureTimestamp = closing.TseSignatureTimestamp,
            IsSimulated = closing.IsSimulated,
            TransactionCount = closing.TransactionCount,
            CashierName = cashierName
                        ?? (string.IsNullOrWhiteSpace(closing.CashierName) ? null : closing.CashierName)
                        ?? closing.User?.UserName
                        ?? closing.User?.Email,
            ShiftNumber = shiftNumber
                        ?? (closing.ShiftNumber > 0
                            ? closing.ShiftNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : null),
            HasStartbeleg = cloud.HasStartbeleg,
            HasMonatsbeleg = cloud.HasMonatsbeleg,
            HasJahresbeleg = cloud.HasJahresbeleg,
            TseProviderLabel = cloud.TseProviderLabel,
            DepExportStatusLabel = cloud.DepExportStatusLabel,
            TseSignatureVerified = cloud.TseSignatureVerified,
        };
    }
}
