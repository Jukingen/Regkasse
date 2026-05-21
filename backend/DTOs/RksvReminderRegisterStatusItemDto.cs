namespace KasseAPI_Final.DTOs;

/// <summary>Per-register unified RKSV reminder status for dashboard overview (single HTTP round-trip).</summary>
public sealed class RksvReminderRegisterStatusItemDto
{
    public Guid CashRegisterId { get; set; }
    public RksvReminderStatusDto Status { get; set; } = null!;
}
