using Prometheus;

namespace KasseAPI_Final.Services.Metrics;

/// <summary>
/// Prometheus-backed cache metrics: hits, misses, size, and derived hit ratio.
/// </summary>
public class CacheMetricsService : ICacheMetricsService
{
    // Qualify Prometheus.Metrics — this type lives in namespace Services.Metrics.
    private static readonly Counter CacheHits = global::Prometheus.Metrics
        .CreateCounter("cache_hits_total", "Cache hits");

    private static readonly Counter CacheMisses = global::Prometheus.Metrics
        .CreateCounter("cache_misses_total", "Cache misses");

    private static readonly Gauge CacheSize = global::Prometheus.Metrics
        .CreateGauge("cache_size_bytes", "Cache size in bytes");

    private static readonly Gauge CacheHitRatio = global::Prometheus.Metrics
        .CreateGauge("cache_hit_ratio", "Cache hit ratio (hits / (hits + misses))");

    public void RecordHit()
    {
        CacheHits.Inc();
        RefreshHitRatio();
    }

    public void RecordMiss()
    {
        CacheMisses.Inc();
        RefreshHitRatio();
    }

    public void RecordSize(long bytes)
    {
        CacheSize.Set(Math.Max(0, bytes));
    }

    public double GetHitRatio()
    {
        var hits = CacheHits.Value;
        var misses = CacheMisses.Value;
        var total = hits + misses;
        return total > 0 ? hits / total : 0;
    }

    private void RefreshHitRatio() => CacheHitRatio.Set(GetHitRatio());
}
