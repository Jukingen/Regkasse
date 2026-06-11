namespace KasseAPI_Final.DTOs;

public sealed class AdminShiftOverviewDto
{
    public IReadOnlyList<AdminShiftRowDto> ActiveShifts { get; init; } = Array.Empty<AdminShiftRowDto>();
    public IReadOnlyList<AdminShiftRowDto> ShiftHistory { get; init; } = Array.Empty<AdminShiftRowDto>();
    public IReadOnlyList<AdminDailyClosingOverviewRowDto> DailyClosings { get; init; } =
        Array.Empty<AdminDailyClosingOverviewRowDto>();
}

public sealed class AdminShiftRowDto
{
    public Guid Id { get; init; }
    public Guid CashRegisterId { get; init; }
    public string? RegisterNumber { get; init; }
    public string CashierId { get; init; } = string.Empty;
    public string CashierName { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public decimal StartBalance { get; init; }
    public decimal EndBalance { get; init; }
    public decimal TotalSales { get; init; }
    public decimal TotalCash { get; init; }
    public decimal TotalCard { get; init; }
    public decimal Difference { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? DailyClosingId { get; init; }
    public decimal? CashCount { get; init; }
    public string? Notes { get; init; }
}

public sealed class AdminDailyClosingOverviewRowDto
{
    public Guid DailyClosingId { get; init; }
    public Guid? ShiftId { get; init; }
    public Guid CashRegisterId { get; init; }
    public string? RegisterNumber { get; init; }
    public string CashierName { get; init; } = string.Empty;
    public DateTime ClosingDate { get; init; }
    public DateTime? ShiftEndedAt { get; init; }
    public decimal TotalSales { get; init; }
    public decimal TotalCash { get; init; }
    public decimal TotalCard { get; init; }
    public decimal? CashCount { get; init; }
    public decimal Difference { get; init; }
    public decimal FiscalTotalAmount { get; init; }
    public decimal FiscalTotalTaxAmount { get; init; }
    public int FiscalTransactionCount { get; init; }
    public bool HasTseSignature { get; init; }
    public string ShiftStatus { get; init; } = string.Empty;
    public string FiscalStatus { get; init; } = string.Empty;
}
