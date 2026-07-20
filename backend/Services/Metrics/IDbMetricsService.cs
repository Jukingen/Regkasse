namespace KasseAPI_Final.Services.Metrics;

/// <summary>
/// Database query and connection Prometheus metrics.
/// </summary>
public interface IDbMetricsService
{
    Task<T> TrackQueryAsync<T>(string queryType, Func<Task<T>> queryFunc);

    /// <summary>
    /// Records an already-executed query (EF Core interceptor path).
    /// Increments <c>db_queries_total</c> and observes <c>db_query_duration_ms</c>.
    /// </summary>
    void RecordQuery(string queryType, double durationMs);

    void TrackConnection(bool isOpen);
}
