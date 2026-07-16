namespace KasseAPI_Final.Configuration;

public sealed class ShiftAutoCloseOptions
{
    public const string SectionName = "ShiftAutoClose";

    /// <summary>When false, the hosted worker does not auto-close stale open registers.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Registers / shifts open longer than this are auto-closed for inactivity (default 12h).</summary>
    public int MaxOpenDurationHours { get; set; } = 12;

    /// <summary>Hosted service polling interval in minutes.</summary>
    public int CheckIntervalMinutes { get; set; } = 60;
}
