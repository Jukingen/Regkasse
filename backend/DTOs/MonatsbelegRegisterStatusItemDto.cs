namespace KasseAPI_Final.DTOs;

/// <summary>Per-register Monatsbeleg status for dashboard overview (single HTTP round-trip).</summary>
public sealed class MonatsbelegRegisterStatusItemDto
{
    public Guid CashRegisterId { get; set; }
    public MonatsbelegStatusDto Status { get; set; } = null!;
}
