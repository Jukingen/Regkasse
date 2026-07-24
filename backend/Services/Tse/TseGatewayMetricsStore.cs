using System.Collections.Concurrent;

namespace KasseAPI_Final.Services.Tse;

public sealed class TseGatewayEndpointMetrics
{
    public long Requests;
    public long SuccessCount;
    public long FailureCount;
    public long TotalResponseMs;
    public int InFlight;
    public bool? Healthy;
    public DateTime? LastCheckedAtUtc;
}

/// <summary>Process-local counters for the TSE API gateway dashboard.</summary>
public interface ITseGatewayMetricsStore
{
    void BeginRequest(Guid endpointId);
    void EndRequest(Guid endpointId, bool success, long elapsedMs, bool? healthy = null);
    void SetHealthy(Guid endpointId, bool healthy);
    TseGatewayEndpointMetrics GetOrCreate(Guid endpointId);
    IReadOnlyDictionary<Guid, TseGatewayEndpointMetrics> Snapshot();
    int NextRoundRobinIndex(int modulo);
}

public sealed class TseGatewayMetricsStore : ITseGatewayMetricsStore
{
    private readonly ConcurrentDictionary<Guid, TseGatewayEndpointMetrics> _byEndpoint = new();
    private long _roundRobin;

    public void BeginRequest(Guid endpointId)
    {
        var m = GetOrCreate(endpointId);
        Interlocked.Increment(ref m.InFlight);
    }

    public void EndRequest(Guid endpointId, bool success, long elapsedMs, bool? healthy = null)
    {
        var m = GetOrCreate(endpointId);
        Interlocked.Decrement(ref m.InFlight);
        Interlocked.Increment(ref m.Requests);
        if (success)
            Interlocked.Increment(ref m.SuccessCount);
        else
            Interlocked.Increment(ref m.FailureCount);
        Interlocked.Add(ref m.TotalResponseMs, Math.Max(0, elapsedMs));
        if (healthy.HasValue)
        {
            m.Healthy = healthy.Value;
            m.LastCheckedAtUtc = DateTime.UtcNow;
        }
    }

    public void SetHealthy(Guid endpointId, bool healthy)
    {
        var m = GetOrCreate(endpointId);
        m.Healthy = healthy;
        m.LastCheckedAtUtc = DateTime.UtcNow;
    }

    public TseGatewayEndpointMetrics GetOrCreate(Guid endpointId) =>
        _byEndpoint.GetOrAdd(endpointId, _ => new TseGatewayEndpointMetrics());

    public IReadOnlyDictionary<Guid, TseGatewayEndpointMetrics> Snapshot() =>
        new Dictionary<Guid, TseGatewayEndpointMetrics>(_byEndpoint);

    public int NextRoundRobinIndex(int modulo)
    {
        if (modulo <= 0)
            return 0;
        var n = Interlocked.Increment(ref _roundRobin);
        return (int)((n - 1) % modulo);
    }
}
