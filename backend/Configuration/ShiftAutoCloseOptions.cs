namespace KasseAPI_Final.Configuration;

public sealed class ShiftAutoCloseOptions
{
    public const string SectionName = "ShiftAutoClose";

    /// <summary>When false, the hosted worker does not auto-close stale open registers.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Registers open longer than this are auto-closed (default 24h).</summary>
    public int MaxOpenDurationHours { get; set; } = 24;

    /// <summary>Hosted service polling interval in minutes.</summary>
    public int CheckIntervalMinutes { get; set; } = 60;
}
