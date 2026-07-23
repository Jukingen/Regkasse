namespace KasseAPI_Final.Configuration;

public sealed class TemporaryPermissionsOptions
{
    public const string SectionName = "TemporaryPermissions";

    /// <summary>Hours before ExpiresAt to emit expiring-soon activity.</summary>
    public int ExpiringSoonHours { get; set; } = 48;

    /// <summary>Background poll interval in minutes.</summary>
    public int PollIntervalMinutes { get; set; } = 15;
}
