namespace KasseAPI_Final.DTOs;

public sealed class AdminForceCloseShiftRequest
{
    public decimal? ClosingBalance { get; set; }
    public string? Reason { get; set; }
}

public sealed class AdminForceCloseShiftResponse
{
    public Guid CashRegisterId { get; init; }
    public int ClosedShiftCount { get; init; }
}
