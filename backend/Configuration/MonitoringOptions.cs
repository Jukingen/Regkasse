namespace KasseAPI_Final.Configuration;

/// <summary>Prometheus / FA monitoring surface configuration.</summary>
public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>Master switch for HTTP metrics middleware and scrape/summary APIs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Prometheus exposition path (default <c>/metrics</c>).</summary>
    public string MetricsEndpoint { get; set; } = "/metrics";

    public PrometheusMonitoringOptions Prometheus { get; set; } = new();
}

public sealed class PrometheusMonitoringOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Suggested scrape interval for operators (not enforced by the API).</summary>
    public int ScrapeIntervalSeconds { get; set; } = 15;
}
