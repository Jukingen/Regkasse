namespace KasseAPI_Final.Services.Metrics;

/// <summary>
/// Cache hit/miss/size Prometheus metrics for <see cref="Cache.ICacheService"/>.
/// </summary>
public interface ICacheMetricsService
{
    void RecordHit();
    void RecordMiss();
    void RecordSize(long bytes);
    double GetHitRatio();
}
