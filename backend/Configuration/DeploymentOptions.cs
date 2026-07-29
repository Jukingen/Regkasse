namespace KasseAPI_Final.Configuration;

/// <summary>
/// Release-stage metadata for safe promotions (orthogonal to <c>ASPNETCORE_ENVIRONMENT</c>).
/// Bind from <c>Deployment</c> section or env <c>Deployment__ReleaseStage</c> / <c>RELEASE_STAGE</c>.
/// </summary>
public sealed class DeploymentOptions
{
    public const string SectionName = "Deployment";

    /// <summary>
    /// Canonical values: <c>dev</c>, <c>staging</c>, <c>canary</c>, <c>production</c>.
    /// Empty → derived from <c>ASPNETCORE_ENVIRONMENT</c> (and optional canary tenant list).
    /// </summary>
    public string ReleaseStage { get; set; } = string.Empty;

    /// <summary>
    /// When the host is Production (or stage is production) but these tenant IDs are ambient,
    /// effective release stage becomes <c>canary</c> for UI banners.
    /// </summary>
    public Guid[] CanaryTenantIds { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// Same as <see cref="CanaryTenantIds"/> but matched against ambient tenant slug (case-insensitive).
    /// </summary>
    public string[] CanaryTenantSlugs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Shared secret for <c>POST /api/webhooks/deployments/ci-report</c>
    /// (GitHub secret <c>DEPLOYMENT_STATUS_TOKEN</c>). Empty → CI reports rejected.
    /// </summary>
    public string StatusReportToken { get; set; } = string.Empty;

    /// <summary>
    /// Per-stage rollback webhook URLs for Super Admin FA rollback button.
    /// Keys: staging | canary | production.
    /// </summary>
    public Dictionary<string, string> RollbackWebhooks { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Default canary soak window in hours (24–48 recommended).</summary>
    public int CanaryDefaultSoakHours { get; set; } = 24;

    public CanaryMonitorOptions CanaryMonitor { get; set; } = new();
}

/// <summary>Background monitoring for canary tenant error volume / rate.</summary>
public sealed class CanaryMonitorOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>How often to evaluate canary tenants.</summary>
    public int CheckIntervalMinutes { get; set; } = 15;

    /// <summary>Audit lookback window for error counting.</summary>
    public int WindowMinutes { get; set; } = 60;

    /// <summary>Absolute failed audit count in the window that triggers an alert.</summary>
    public int ErrorCountThreshold { get; set; } = 10;

    /// <summary>Failed / total audit % in the window that triggers a high-error-rate alert.</summary>
    public double ErrorRateThresholdPercent { get; set; } = 5.0;

    /// <summary>Minimum total audit events before rate alert applies.</summary>
    public int MinEventsForRate { get; set; } = 20;
}
