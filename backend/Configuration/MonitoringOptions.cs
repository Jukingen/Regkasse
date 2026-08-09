namespace KasseAPI_Final.Configuration;

/// <summary>Prometheus / FA monitoring surface configuration.</summary>
public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>Master switch for HTTP metrics middleware and scrape/summary APIs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Prometheus exposition path (default <c>/metrics</c>).</summary>
    public string MetricsEndpoint { get; set; } = "/metrics";

    /// <summary>
    /// Log a Warning when a non-exempt request exceeds this duration (milliseconds).
    /// Set to <c>0</c> to disable slow-request warnings. Default <c>1000</c>.
    /// </summary>
    public int SlowRequestThresholdMs { get; set; } = 1000;

    public PrometheusMonitoringOptions Prometheus { get; set; } = new();
}

public sealed class PrometheusMonitoringOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Suggested scrape interval for operators (not enforced by the API).</summary>
    public int ScrapeIntervalSeconds { get; set; } = 15;
}
