using System.Collections.Concurrent;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tse;

/// <summary>In-memory Development-only TSE simulation overlays (latency + restore snapshots).</summary>
public interface ITseSimulatorStateStore
{
    int GetLatencyMs(Guid deviceId);
    void SetLatencyMs(Guid deviceId, int latencyMs);
    void ClearLatency(Guid deviceId);

    string? GetActiveScenarioId(Guid deviceId);
    void SetActiveScenario(Guid deviceId, string? scenarioId);

    void SaveBaseline(Guid deviceId, TseSimulatorBaseline baseline);
    bool TryGetBaseline(Guid deviceId, out TseSimulatorBaseline baseline);
    void Clear(Guid deviceId);
}

public sealed class TseSimulatorBaseline
{
    public bool IsConnected { get; init; }
    public bool CanCreateInvoices { get; init; }
    public string CertificateStatus { get; init; } = "VALID";
    public string MemoryStatus { get; init; } = "OK";
    public string? ErrorMessage { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? IssuedAt { get; init; }
}

public sealed class TseSimulatorStateStore : ITseSimulatorStateStore
{
    private readonly ConcurrentDictionary<Guid, int> _latency = new();
    private readonly ConcurrentDictionary<Guid, string> _scenario = new();
    private readonly ConcurrentDictionary<Guid, TseSimulatorBaseline> _baselines = new();

    public int GetLatencyMs(Guid deviceId) =>
        _latency.TryGetValue(deviceId, out var ms) ? Math.Max(0, ms) : 0;

    public void SetLatencyMs(Guid deviceId, int latencyMs)
    {
        if (latencyMs <= 0)
            _latency.TryRemove(deviceId, out _);
        else
            _latency[deviceId] = Math.Clamp(latencyMs, 1, 60_000);
    }

    public void ClearLatency(Guid deviceId) => _latency.TryRemove(deviceId, out _);

    public string? GetActiveScenarioId(Guid deviceId) =>
        _scenario.TryGetValue(deviceId, out var id) ? id : null;

    public void SetActiveScenario(Guid deviceId, string? scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
            _scenario.TryRemove(deviceId, out _);
        else
            _scenario[deviceId] = scenarioId.Trim();
    }

    public void SaveBaseline(Guid deviceId, TseSimulatorBaseline baseline) =>
        _baselines[deviceId] = baseline;

    public bool TryGetBaseline(Guid deviceId, out TseSimulatorBaseline baseline) =>
        _baselines.TryGetValue(deviceId, out baseline!);

    public void Clear(Guid deviceId)
    {
        _latency.TryRemove(deviceId, out _);
        _scenario.TryRemove(deviceId, out _);
        _baselines.TryRemove(deviceId, out _);
    }
}
