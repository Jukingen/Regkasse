using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.Reports;

namespace KasseAPI_Final.DTOs;

public sealed class StartShiftRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [Range(0, 999_999_999)]
    public decimal StartBalance { get; set; }
}

public sealed class AutoOpenShiftRequest
{
    /// <summary>
    /// Optional register id. When omitted or empty, auto-open resolves
    /// <see cref="KasseAPI_Final.Models.UserSettings.CashRegisterId"/> for the caller.
    /// </summary>
    public string? CashRegisterId { get; set; }
}

/// <summary>Machine-readable outcomes for <c>POST /api/pos/shift/auto-open</c>.</summary>
public static class ShiftAutoOpenCodes
{
    public const string Success = "SUCCESS";
    /// <summary>Alias of <see cref="Success"/> for older POS clients.</summary>
    public const string Ok = Success;
    public const string NeedRegisterSelection = "NEED_REGISTER_SELECTION";
    public const string RegisterUnavailable = "REGISTER_UNAVAILABLE";
    public const string RegisterNotFound = "REGISTER_NOT_FOUND";
    public const string ShiftAlreadyOpen = "SHIFT_ALREADY_OPEN";
    public const string RegisterDecommissioned = "REGISTER_DECOMMISSIONED";
    public const string NoActiveRegisters = "NO_ACTIVE_REGISTERS";
}

/// <summary>German POS-facing copy for auto-open (default locale de-DE).</summary>
public static class ShiftAutoOpenMessages
{
    public const string ShiftOpened = "Schicht erfolgreich geöffnet.";
    public const string NeedRegisterSelection =
        "Bitte wählen Sie eine Kasse aus, bevor Sie fortfahren.";
    public const string RegisterUnavailable =
        "Die ausgewählte Kasse ist nicht verfügbar. Bitte kontaktieren Sie den Administrator.";
    public const string RegisterNotFound = "Die ausgewählte Kasse wurde nicht gefunden.";
    public const string ShiftAlreadyOpen = "Die Schicht ist bereits geöffnet.";
    public const string RegisterDecommissioned =
        "Diese Kasse wurde stillgelegt und kann nicht mehr verwendet werden.";
    public const string NoActiveRegisters =
        "Keine aktiven Kassen gefunden. Bitte kontaktieren Sie den Administrator.";
}

/// <summary>
/// Structured auto-open result. HTTP 200 when <see cref="Success"/> is true
/// (including <see cref="ShiftAutoOpenCodes.ShiftAlreadyOpen"/>); HTTP 400 otherwise.
/// </summary>
public sealed class ShiftAutoOpenResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public CashierShiftDto? Data { get; init; }

    public static ShiftAutoOpenResult Opened(CashierShiftDto shift) => new()
    {
        Success = true,
        Code = ShiftAutoOpenCodes.Success,
        Message = ShiftAutoOpenMessages.ShiftOpened,
        Data = shift,
    };

    public static ShiftAutoOpenResult AlreadyOpen(CashierShiftDto shift) => new()
    {
        Success = true,
        Code = ShiftAutoOpenCodes.ShiftAlreadyOpen,
        Message = ShiftAutoOpenMessages.ShiftAlreadyOpen,
        Data = shift,
    };

    public static ShiftAutoOpenResult Fail(string code, string message) => new()
    {
        Success = false,
        Code = code,
        Message = message,
    };
}

public sealed class EndShiftRequest
{
    [Range(0, 999_999_999)]
    public decimal EndBalance { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public sealed class CashierShiftDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid CashRegisterId { get; init; }
    public string CashierId { get; init; } = string.Empty;
    public string CashierName { get; init; } = string.Empty;
    public decimal StartBalance { get; init; }
    public decimal EndBalance { get; init; }
    public decimal TotalSales { get; init; }
    public decimal TotalCash { get; init; }
    public decimal TotalCard { get; init; }
    public decimal Difference { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public Guid? DailyClosingId { get; init; }
    public decimal? CashCount { get; init; }
    public bool IsAutoOpened { get; init; }
    public bool IsAutoClosed { get; init; }
}

public sealed class CurrentShiftResponse
{
    public bool HasActiveShift { get; init; }
    public CashierShiftDto? Shift { get; init; }
}

public sealed class ShiftTotalsDto
{
    public decimal Sales { get; init; }
    public decimal Cash { get; init; }
    public decimal Card { get; init; }
}

/// <summary>Non-fiscal shift closing summary for POS print/preview.</summary>
public sealed class ShiftClosingReceiptDto
{
    public Guid ShiftId { get; init; }
    public string CashierName { get; init; } = string.Empty;
    public string? RegisterNumber { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public decimal StartBalance { get; init; }
    public decimal EndBalance { get; init; }
    public decimal TotalSales { get; init; }
    public decimal TotalCash { get; init; }
    public decimal TotalCard { get; init; }
    public decimal Difference { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class EndShiftResponse
{
    public CashierShiftDto Shift { get; init; } = null!;
    public ShiftClosingReceiptDto Receipt { get; init; } = null!;
}

public sealed class PosDailyClosingRequest
{
    [Range(0, 999_999_999)]
    public decimal CashCount { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public sealed class PosDailyClosingReportDto
{
    public DateTime BusinessDate { get; init; }
    public Guid? CashRegisterId { get; init; }
    public string? RegisterNumber { get; init; }
    public string? CompanyName { get; init; }
    public string? CompanyAddress { get; init; }
    public string? CompanyVatId { get; init; }
    public DateTime? PeriodStartUtc { get; init; }
    public DateTime? PeriodEndUtc { get; init; }
    public string? TseProviderLabel { get; init; }
    public string? DepExportStatusLabel { get; init; }
    public bool TseSignatureVerified { get; init; }
    public bool HasStartbeleg { get; init; }
    public bool HasMonatsbeleg { get; init; }
    public bool HasJahresbeleg { get; init; }
    public decimal TotalSales { get; init; }
    public decimal TotalCash { get; init; }
    public decimal TotalCard { get; init; }
    public decimal TotalVoucherRedemptions { get; init; }
    public decimal TotalOtherPaymentMethods { get; init; }
    public decimal CashCount { get; init; }
    public decimal Difference { get; init; }
    public decimal FiscalTotalAmount { get; init; }
    public decimal FiscalTotalTaxAmount { get; init; }
    public decimal FiscalTotalNetAmount { get; init; }
    public int FiscalTransactionCount { get; init; }
    public string? TseSignature { get; init; }
    /// <summary>Compact JWS of the previous completed daily closing (signature chain).</summary>
    public string? PreviousClosingSignature { get; init; }
    public string? CashierName { get; init; }
    /// <summary>RKSV Schicht-Nr. (short cashier shift id).</summary>
    public string? ShiftNumber { get; init; }
    public DailyClosingTaxBreakdownDto TaxBreakdown { get; init; } = new();
    public PaymentBreakdown PaymentBreakdown { get; init; } = new();
    public bool IsDemoFiscal { get; init; }
    public string FiscalEnvironment { get; init; } = "Production";
    public string TseStatusLabel { get; init; } = "TSE OK";
    /// <summary>Short badge: TSE AKTIV / TSE SIMULIERT.</summary>
    public string TseStatusBadge { get; init; } = string.Empty;
    public string RksvFooterLabel { get; init; } = string.Empty;
    /// <summary>RKSV §9 QR wire format in production; NON_FISCAL_DEMO marker in demo.</summary>
    public string? QrPayload { get; init; }
    /// <summary>Daily, Monthly, or Yearly — drives localized PDF title.</summary>
    public string ClosingType { get; init; } = "Daily";
    public string SnapshotDisclaimerDe { get; init; } =
        DailyClosingReportComposer.RksvDailyDisclaimerDe;
    /// <summary>Plain-text late-closing note when <c>DailyClosing.IsBackdated</c>.</summary>
    public string? BackdatedNotice { get; init; }
    /// <summary>Shown when payment-row Umsatz and signed fiscal gross diverge.</summary>
    public string? SalesFiscalReconciliationNote { get; init; }
    /// <summary>Explains shift-scoped cash difference vs calendar-day cash sales.</summary>
    public string? DifferenceScopeNote { get; init; }
    public TransactionBreakdown TransactionBreakdown { get; init; } = new();
}

public sealed class PosDailyClosingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int PaymentsWithoutInvoiceCount { get; init; }
    public CashierShiftDto? Shift { get; init; }
    public Guid? DailyClosingId { get; init; }
    public PosDailyClosingReportDto? Report { get; init; }
}

public static class PosDailyClosingBlockReasons
{
    public const string AlreadyClosedToday = "already_closed_today";
    public const string PaymentsWithoutInvoice = "payments_without_invoice";
    public const string RegisterUnavailable = "register_unavailable";
    public const string NoActiveShift = "no_active_shift";
}

public sealed class PosDailyClosingStatusDto
{
    public bool CanClose { get; init; }
    public bool HasActiveShift { get; init; }
    public string Message { get; init; } = string.Empty;
    /// <summary>Machine-readable block reason for POS i18n (<see cref="PosDailyClosingBlockReasons"/>).</summary>
    public string? BlockReason { get; init; }
    public DateTime? LastClosingDate { get; init; }
    /// <summary>When the latest daily closing was performed (UTC <see cref="DailyClosing.CreatedAt"/>).</summary>
    public DateTime? LastClosingPerformedAt { get; init; }
    public int PaymentsWithoutInvoiceCount { get; init; }
}
