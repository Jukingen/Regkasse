using System.Diagnostics;
using Prometheus;

namespace KasseAPI_Final.Services.Metrics;

/// <summary>
/// Prometheus-backed database metrics: query duration/count and active connections.
/// </summary>
public class DbMetricsService : IDbMetricsService
{
    // Use global::Prometheus.Metrics — this type lives in namespace Services.Metrics.
    private static readonly Histogram QueryDuration = global::Prometheus.Metrics
        .CreateHistogram("db_query_duration_ms", "Database query duration",
            new HistogramConfiguration
            {
                LabelNames = ["query_type"],
                Buckets = [5, 10, 25, 50, 100, 250, 500, 1000]
            });

    private static readonly Counter QueryCounter = global::Prometheus.Metrics
        .CreateCounter("db_queries_total", "Total database queries",
            new CounterConfiguration
            {
                LabelNames = ["query_type"]
            });

    private static readonly Gauge DbConnections = global::Prometheus.Metrics
        .CreateGauge("db_connections_active", "Active database connections");

    public async Task<T> TrackQueryAsync<T>(string queryType, Func<Task<T>> queryFunc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);
        ArgumentNullException.ThrowIfNull(queryFunc);

        var label = NormalizeQueryType(queryType);
        var stopwatch = Stopwatch.StartNew();
        QueryCounter.WithLabels(label).Inc();

        try
        {
            return await queryFunc().ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            QueryDuration.WithLabels(label).Observe(stopwatch.ElapsedMilliseconds);
        }
    }

    public void RecordQuery(string queryType, double durationMs)
    {
        var label = NormalizeQueryType(queryType);
        QueryCounter.WithLabels(label).Inc();
        if (durationMs >= 0)
            QueryDuration.WithLabels(label).Observe(durationMs);
    }

    public void TrackConnection(bool isOpen)
    {
        if (isOpen)
            DbConnections.Inc();
        else
            DbConnections.Dec();
    }

    private static string NormalizeQueryType(string queryType) =>
        string.IsNullOrWhiteSpace(queryType) ? "other" : queryType.Trim().ToLowerInvariant();
}
