namespace KasseAPI_Final.Configuration;

/// <summary>
/// Human-attested go-live flags that cannot be proven from running config alone
/// (AVV, on-call, Alertmanager host render, GO_LIVE_CHECKLIST §8).
/// Defaults are fail-closed (false). Do not set true without named-human evidence.
/// </summary>
public sealed class GoLiveOptions
{
    public const string SectionName = "GoLive";

    /// <summary>
    /// Host Alertmanager receivers are rendered (not the tracked null file) and a routing test succeeded.
    /// </summary>
    public bool AlertmanagerReceiversConfigured { get; set; }

    /// <summary>AVV / DPA signed for the first paying pilots.</summary>
    public bool AvvSignedForPilots { get; set; }

    /// <summary>On-call rota named for the first customers.</summary>
    public bool OnCallNamed { get; set; }

    /// <summary>
    /// <c>docs/GO_LIVE_CHECKLIST.md</c> §8 signed by named humans.
    /// Agents must not set this true.
    /// </summary>
    public bool Section8Signed { get; set; }
}
