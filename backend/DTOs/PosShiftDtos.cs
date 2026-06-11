using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class StartShiftRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [Range(0, 999_999_999)]
    public decimal StartBalance { get; set; }
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
    public string? RegisterNumber { get; init; }
    public decimal TotalSales { get; init; }
    public decimal TotalCash { get; init; }
    public decimal TotalCard { get; init; }
    public decimal CashCount { get; init; }
    public decimal Difference { get; init; }
    public decimal FiscalTotalAmount { get; init; }
    public decimal FiscalTotalTaxAmount { get; init; }
    public int FiscalTransactionCount { get; init; }
    public string? TseSignature { get; init; }
    public string SnapshotDisclaimerDe { get; init; } =
        "Übersicht aus Zahlungszeilen — kein Ersatz für den operativen Tagesabschluss oder formale RKSV-Berichte.";
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

public sealed class PosDailyClosingStatusDto
{
    public bool CanClose { get; init; }
    public bool HasActiveShift { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime? LastClosingDate { get; init; }
    public int PaymentsWithoutInvoiceCount { get; init; }
}
