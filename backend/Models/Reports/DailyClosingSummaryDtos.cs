using System;
using System.Collections.Generic;

namespace KasseAPI_Final.Models.Reports;

/// <summary>
/// Admin daily closing snapshot from <c>payment_details</c> (read-only, not a legal Z-report).
/// </summary>
public sealed class DailyClosingSummaryDto
{
    /// <summary>Austria business calendar date (date component only).</summary>
    public DateTime BusinessDate { get; set; }

    /// <summary>When set, the summary is restricted to this register; otherwise all tenant registers.</summary>
    public Guid? CashRegisterId { get; set; }

    public decimal TotalSales { get; set; }

    public decimal TotalVoucherRedemptions { get; set; }

    public decimal TotalCash { get; set; }

    public decimal TotalCard { get; set; }

    public PaymentBreakdown PaymentBreakdown { get; set; } = new();

    /// <summary>Other payment methods on normal sale rows (e.g. mobile, bank transfer).</summary>
    public decimal TotalOtherPaymentMethods { get; set; }

    /// <summary>Invoice-authoritative gross (same filter as RKSV Tagesabschluss signing).</summary>
    public decimal FiscalTotalAmount { get; set; }

    public decimal FiscalTotalTaxAmount { get; set; }

    public int FiscalTransactionCount { get; set; }

    /// <summary>Payment-row sales total minus <see cref="FiscalTotalAmount"/> when they diverge.</summary>
    public decimal SalesFiscalDelta { get; set; }

    public int ReceiptCount { get; set; }

    public int StornoRowCount { get; set; }

    public decimal StornoTotalAmount { get; set; }

    public IReadOnlyList<DailyClosingSummaryLineDto> SpecialReceipts { get; set; } = Array.Empty<DailyClosingSummaryLineDto>();

    public IReadOnlyList<DailyClosingSummaryLineDto> Stornos { get; set; } = Array.Empty<DailyClosingSummaryLineDto>();

    public TransactionBreakdown TransactionBreakdown { get; set; } = new();

    /// <summary>Invoice-authoritative VAT buckets (20% / 10% / 0%).</summary>
    public DailyClosingTaxBreakdownDto TaxBreakdown { get; set; } = new();

    /// <summary>German operator hint (UI may show as-is).</summary>
    public string SnapshotDisclaimerDe { get; set; } =
        "Übersicht aus Zahlungszeilen — kein Ersatz für den operativen Tagesabschluss oder formale RKSV-Berichte.";
}

public sealed class DailyClosingSummaryLineDto
{
    public Guid Id { get; set; }

    public Guid CashRegisterId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    /// <summary>Resolved enum name (Cash, Card, Voucher, …).</summary>
    public string PaymentMethod { get; set; } = "Unknown";

    public string? RksvSpecialReceiptKind { get; set; }

    public bool IsStorno { get; set; }

    public string? StornoReason { get; set; }

    public Guid? OriginalReceiptId { get; set; }
}
