namespace KasseAPI_Final.Configuration;

/// <summary>
/// Hosted Peppol Access Point. <c>AccessPointMode=own</c> is rejected.
/// <c>Provider=not-configured</c> validates XML and does not send.
/// Secrets: <c>Peppol__ApiKey</c>. Do not commit certificates.
/// </summary>
public sealed class PeppolOptions
{
    public const string SectionName = "Peppol";

    public string AccessPointMode { get; set; } = "hosted";

    public string Environment { get; set; } = "TEST";

    public string Provider { get; set; } = "not-configured";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Minutes between ACK polls of <c>Sent</c> canary rows. <c>0</c> disables the poller.
    /// No Storecove HTTP while this is 0.
    /// </summary>
    public int AckPollInterval { get; set; }

    /// <summary>
    /// Seconds to wait before each ACK retry after a transient Storecove <c>ERROR</c>.
    /// Default is 5 minutes then 30 minutes. Only exact <c>TEST</c> polls.
    /// </summary>
    public int[] AckRetryIntervalsSeconds { get; set; } = [300, 1800];

    /// <summary>
    /// Ops record for a future move out of <c>FeatureFlagNames.Reserved</c>.
    /// Read at startup. This process does not write these fields.
    /// Canary <c>SubmitAsync</c> may open TEST HTTP for <see cref="PeppolReservedExitOptions.CanaryTenantId"/> only.
    /// </summary>
    public PeppolReservedExitOptions ReservedExit { get; set; } = new();

    /// <summary>
    /// Storecove settings. <c>ApiKey</c> binds from <c>Peppol__Storecove__ApiKey</c> only.
    /// Do not put that secret in appsettings.
    /// </summary>
    public PeppolStorecoveOptions Storecove { get; set; } = new();
}

/// <summary>
/// Transitional canary gate. <c>Enabled</c> defaults false. The Peppol flag stays in
/// <c>FeatureFlagNames.Reserved</c> until a later package.
/// </summary>
public sealed class PeppolReservedExitOptions
{
    public bool Enabled { get; set; }

    public string CanaryTenantId { get; set; } = string.Empty;

    public DateTime? TestAckReceivedAtUtc { get; set; }

    public string ApprovedBy { get; set; } = string.Empty;

    public DateTime? ApprovedAtUtc { get; set; }
}

/// <summary>
/// <c>Peppol:Storecove</c>. Used only when <c>Peppol:Provider=storecove</c>.
/// <see cref="ApiKey"/> is a deployment secret (<c>Peppol__Storecove__ApiKey</c>).
/// </summary>
public sealed class PeppolStorecoveOptions
{
    public const string SectionName = "Storecove";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary><c>TEST</c> or <c>LIVE</c>. <c>LIVE</c> does not open HTTP in 22-b-1.</summary>
    public string Environment { get; set; } = "TEST";
}
