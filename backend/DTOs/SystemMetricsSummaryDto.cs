namespace KasseAPI_Final.DTOs;

/// <summary>FA dashboard system metrics summary (derived from Prometheus + live DB counts).</summary>
public sealed class SystemMetricsSummaryDto
{
    public long TotalRequests { get; init; }
    public double AvgResponseTime { get; init; }
    public double ErrorRate { get; init; }
    public int ActiveUsers { get; init; }
    public int ActiveOrders { get; init; }
    public int ActiveTenants { get; init; }
    public double CacheHitRatio { get; init; }
    public long Uptime { get; init; }
    public string Environment { get; init; } = string.Empty;
}
