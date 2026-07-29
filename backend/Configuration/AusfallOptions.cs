namespace KasseAPI_Final.Configuration;

/// <summary>Ausfall / Wiederinbetriebnahme FON reporting policy (P0-3).</summary>
public sealed class AusfallOptions
{
    public const string SectionName = "Ausfall";

    /// <summary>
    /// When true, failover/health suggestions enqueue FON outbox immediately.
    /// Default false: create Suggested episode for operator approval.
    /// </summary>
    public bool AutoEnqueue { get; set; }

    /// <summary>
    /// Minimum continuous offline/unhealthy minutes before auto-suggestion from health (not used for immediate failover hooks).
    /// </summary>
    public int AusfallGraceMinutes { get; set; } = 30;
}
