using System.Diagnostics;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Load-balances operational HealthProbe requests across configured TSE provider backends.
/// Never invokes fiscal Sign / Startbeleg.
/// </summary>
public sealed class TseApiGatewayService : ITseApiGatewayService
{
    private readonly AppDbContext _db;
    private readonly ITseProviderFactory _providers;
    private readonly ITseGatewayMetricsStore _metrics;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<TseApiGatewayService> _logger;

    public TseApiGatewayService(
        AppDbContext db,
        ITseProviderFactory providers,
        ITseGatewayMetricsStore metrics,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<TseApiGatewayService> logger)
    {
        _db = db;
        _providers = providers;
        _metrics = metrics;
        _tseOptions = tseOptions;
        _logger = logger;
    }

    public async Task<TseGatewayConfigDto> GetGatewayConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetOrCreateConfigAsync(actorUserId: null, cancellationToken).ConfigureAwait(false);
        return MapConfig(config, includeLiveMetrics: false);
    }

    public async Task<TseGatewayConfigDto> ConfigureGatewayAsync(
        ConfigureTseGatewayRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TseLoadBalancingStrategies.IsValid(request.Strategy))
            throw new ArgumentException(
                $"Invalid strategy. Allowed: {string.Join(", ", TseLoadBalancingStrategies.All)}");

        var config = await GetOrCreateConfigAsync(actorUserId, cancellationToken).ConfigureAwait(false);
        config.Strategy = TseLoadBalancingStrategies.Normalize(request.Strategy);
        config.HealthCheckIntervalSeconds = Math.Clamp(request.HealthCheckInterval, 5, 3600);
        config.TimeoutMs = Math.Clamp(request.Timeout, 500, 60_000);
        config.RetryCount = Math.Clamp(request.RetryCount, 0, 10);
        config.Enabled = request.Enabled;
        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedBy = Truncate(actorUserId, 450);

        var incoming = request.Endpoints ?? new List<ConfigureTseGatewayEndpointRequestDto>();
        if (incoming.Count == 0)
            throw new ArgumentException("At least one endpoint is required.");

        var existing = await _db.TseGatewayEndpoints
            .Where(e => e.ConfigId == config.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _db.TseGatewayEndpoints.RemoveRange(existing);

        var order = 0;
        foreach (var ep in incoming)
        {
            var provider = TseOptions.NormalizeProviderName(ep.Provider);
            if (string.IsNullOrEmpty(provider) || !_providers.GetKnownProviderNames().Contains(provider, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"Unknown provider '{ep.Provider}'.");

            var url = string.IsNullOrWhiteSpace(ep.Endpoint)
                ? DefaultEndpointUrl(provider)
                : ep.Endpoint.Trim();

            _db.TseGatewayEndpoints.Add(new TseGatewayEndpoint
            {
                Id = ep.Id is { } id && id != Guid.Empty ? id : Guid.NewGuid(),
                ConfigId = config.Id,
                Provider = provider,
                EndpointUrl = Truncate(url, 512)!,
                Weight = Math.Clamp(ep.Weight <= 0 ? 1 : ep.Weight, 1, 100),
                Enabled = ep.Enabled,
                SortOrder = ep.SortOrder != 0 ? ep.SortOrder : order++,
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        config = await _db.TseGatewayConfigs
            .Include(c => c.Endpoints)
            .FirstAsync(c => c.Id == config.Id, cancellationToken)
            .ConfigureAwait(false);

        return MapConfig(config, includeLiveMetrics: true);
    }

    public async Task<TseGatewayStatusDto> GetGatewayStatusAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetOrCreateConfigAsync(actorUserId: null, cancellationToken).ConfigureAwait(false);
        var endpointDtos = MapEndpoints(config.Endpoints.OrderBy(e => e.SortOrder).ToList(), includeLiveMetrics: true);
        var snap = _metrics.Snapshot();

        long total = 0, ok = 0, totalMs = 0;
        foreach (var ep in config.Endpoints)
        {
            if (!snap.TryGetValue(ep.Id, out var m))
                continue;
            total += Interlocked.Read(ref m.Requests);
            ok += Interlocked.Read(ref m.SuccessCount);
            totalMs += Interlocked.Read(ref m.TotalResponseMs);
        }

        return new TseGatewayStatusDto
        {
            Enabled = config.Enabled,
            Strategy = config.Strategy,
            Stats = new TseGatewayStatsDto
            {
                TotalRequests = total,
                SuccessRate = total == 0 ? 100 : Math.Round(100.0 * ok / total, 2),
                AvgResponseTime = total == 0 ? 0 : Math.Round((double)totalMs / total, 2),
            },
            Endpoints = endpointDtos,
            GeneratedAt = DateTime.UtcNow,
        };
    }

    public async Task<TseGatewayResponseDto> RouteRequestAsync(
        TseGatewayRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operation = string.IsNullOrWhiteSpace(request.Operation)
            ? TseGatewayOperations.HealthProbe
            : request.Operation.Trim();

        if (!string.Equals(operation, TseGatewayOperations.HealthProbe, StringComparison.OrdinalIgnoreCase))
        {
            return new TseGatewayResponseDto
            {
                Success = false,
                Operation = operation,
                Message = "Only HealthProbe is supported. Fiscal Sign is not routed through the API gateway.",
                CorrelationId = request.CorrelationId,
                SimulationOnly = true,
            };
        }

        var config = await GetOrCreateConfigAsync(actorUserId, cancellationToken).ConfigureAwait(false);
        if (!config.Enabled)
        {
            return new TseGatewayResponseDto
            {
                Success = false,
                Operation = TseGatewayOperations.HealthProbe,
                Message = "Gateway is disabled.",
                CorrelationId = request.CorrelationId,
                SimulationOnly = true,
            };
        }

        var candidates = config.Endpoints
            .Where(e => e.Enabled)
            .OrderBy(e => e.SortOrder)
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.PreferredProvider))
        {
            var preferred = TseOptions.NormalizeProviderName(request.PreferredProvider);
            candidates = candidates
                .Where(e => string.Equals(e.Provider, preferred, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (candidates.Count == 0)
        {
            return new TseGatewayResponseDto
            {
                Success = false,
                Operation = TseGatewayOperations.HealthProbe,
                Message = "No enabled gateway endpoints match the request.",
                CorrelationId = request.CorrelationId,
                SimulationOnly = true,
            };
        }

        var maxAttempts = Math.Max(1, config.RetryCount + 1);
        var ordered = OrderCandidates(candidates, config.Strategy);
        var swTotal = Stopwatch.StartNew();
        var attempts = 0;
        Exception? lastError = null;

        foreach (var endpoint in ordered.Take(maxAttempts))
        {
            attempts++;
            _metrics.BeginRequest(endpoint.Id);
            var sw = Stopwatch.StartNew();
            try
            {
                var provider = _providers.GetProvider(endpoint.Provider);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(config.TimeoutMs);

                var ready = await provider.IsReadyAsync(timeoutCts.Token).ConfigureAwait(false);
                sw.Stop();
                _metrics.EndRequest(endpoint.Id, ready, sw.ElapsedMilliseconds, healthy: ready);

                if (ready)
                {
                    swTotal.Stop();
                    return new TseGatewayResponseDto
                    {
                        Success = true,
                        Operation = TseGatewayOperations.HealthProbe,
                        SelectedProvider = endpoint.Provider,
                        SelectedEndpoint = endpoint.EndpointUrl,
                        SelectedEndpointId = endpoint.Id,
                        Attempts = attempts,
                        ElapsedMs = swTotal.ElapsedMilliseconds,
                        Message = $"HealthProbe succeeded via {endpoint.Provider}.",
                        CorrelationId = request.CorrelationId,
                        SimulationOnly = true,
                    };
                }

                lastError = new InvalidOperationException($"Provider {endpoint.Provider} reported not ready.");
            }
            catch (Exception ex)
            {
                sw.Stop();
                _metrics.EndRequest(endpoint.Id, success: false, sw.ElapsedMilliseconds, healthy: false);
                lastError = ex;
                _logger.LogWarning(ex, "TSE gateway HealthProbe failed for {Provider}", endpoint.Provider);
            }
        }

        swTotal.Stop();
        var message = lastError?.Message ?? "All gateway attempts failed.";
        _logger.LogWarning(
            "TSE gateway HealthProbe exhausted attempts={Attempts} correlation={CorrelationId}: {Message}",
            attempts,
            request.CorrelationId,
            message);

        return new TseGatewayResponseDto
        {
            Success = false,
            Operation = TseGatewayOperations.HealthProbe,
            Attempts = attempts,
            ElapsedMs = swTotal.ElapsedMilliseconds,
            Message = message,
            CorrelationId = request.CorrelationId,
            SimulationOnly = true,
        };
    }

    private List<TseGatewayEndpoint> OrderCandidates(List<TseGatewayEndpoint> candidates, string strategy)
    {
        var normalized = TseLoadBalancingStrategies.Normalize(strategy);
        return normalized switch
        {
            TseLoadBalancingStrategies.LeastConnections => candidates
                .OrderBy(e => _metrics.GetOrCreate(e.Id).InFlight)
                .ThenBy(e => e.SortOrder)
                .ToList(),
            TseLoadBalancingStrategies.Weighted => BuildWeightedOrder(candidates),
            _ => BuildRoundRobinOrder(candidates),
        };
    }

    private List<TseGatewayEndpoint> BuildRoundRobinOrder(List<TseGatewayEndpoint> candidates)
    {
        if (candidates.Count == 0)
            return candidates;
        var start = _metrics.NextRoundRobinIndex(candidates.Count);
        var result = new List<TseGatewayEndpoint>(candidates.Count);
        for (var i = 0; i < candidates.Count; i++)
            result.Add(candidates[(start + i) % candidates.Count]);
        return result;
    }

    private static List<TseGatewayEndpoint> BuildWeightedOrder(List<TseGatewayEndpoint> candidates)
    {
        // Expand by weight then shuffle lightly by sort order; first pick prefers higher weight.
        var expanded = candidates
            .SelectMany(e => Enumerable.Repeat(e, Math.Max(1, e.Weight)))
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .OrderByDescending(e => e.Weight)
            .ThenBy(e => e.SortOrder)
            .ToList();
        return expanded;
    }

    private async Task<TseGatewayConfig> GetOrCreateConfigAsync(
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var config = await _db.TseGatewayConfigs
            .Include(c => c.Endpoints)
            .FirstOrDefaultAsync(c => c.Id == TseGatewayConfig.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (config != null)
            return config;

        config = new TseGatewayConfig
        {
            Id = TseGatewayConfig.SingletonId,
            Strategy = TseLoadBalancingStrategies.RoundRobin,
            HealthCheckIntervalSeconds = 30,
            TimeoutMs = 5000,
            RetryCount = 3,
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedBy = Truncate(actorUserId, 450),
        };

        var sort = 0;
        foreach (var name in _providers.GetKnownProviderNames())
        {
            // Default: include configured / fake / soft; others can be added via Configure.
            if (!_providers.IsProviderConfigured(name)
                && !string.Equals(name, TseOptions.ProviderFake, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, TseOptions.ProviderSoft, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            config.Endpoints.Add(new TseGatewayEndpoint
            {
                Id = Guid.NewGuid(),
                ConfigId = config.Id,
                Provider = name,
                EndpointUrl = DefaultEndpointUrl(name),
                Weight = 1,
                Enabled = true,
                SortOrder = sort++,
            });
        }

        if (config.Endpoints.Count == 0)
        {
            config.Endpoints.Add(new TseGatewayEndpoint
            {
                Id = Guid.NewGuid(),
                ConfigId = config.Id,
                Provider = TseOptions.ProviderFake,
                EndpointUrl = DefaultEndpointUrl(TseOptions.ProviderFake),
                Weight = 1,
                Enabled = true,
                SortOrder = 0,
            });
        }

        _db.TseGatewayConfigs.Add(config);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return config;
    }

    private string DefaultEndpointUrl(string provider)
    {
        var opts = _tseOptions.CurrentValue;
        if (opts.Providers != null
            && opts.Providers.TryGetValue(provider, out var vendor)
            && !string.IsNullOrWhiteSpace(vendor.ApiBaseUrl))
        {
            return vendor.ApiBaseUrl.Trim();
        }

        return provider switch
        {
            TseOptions.ProviderFiskaly => "https://kassensichv.io/api/v1",
            TseOptions.ProviderEpson => "https://epson-tse.local/",
            TseOptions.ProviderSwissbit => "https://swissbit-tse.local/",
            TseOptions.ProviderFake => "local://fake-tse",
            TseOptions.ProviderSoft => "local://soft-tse",
            _ => $"local://{provider}",
        };
    }

    private TseGatewayConfigDto MapConfig(TseGatewayConfig config, bool includeLiveMetrics) =>
        new()
        {
            Strategy = config.Strategy,
            HealthCheckInterval = config.HealthCheckIntervalSeconds,
            Timeout = config.TimeoutMs,
            RetryCount = config.RetryCount,
            Enabled = config.Enabled,
            UpdatedAt = config.UpdatedAt,
            Endpoints = MapEndpoints(
                config.Endpoints.OrderBy(e => e.SortOrder).ToList(),
                includeLiveMetrics),
        };

    private IReadOnlyList<TseGatewayEndpointDto> MapEndpoints(
        IReadOnlyList<TseGatewayEndpoint> endpoints,
        bool includeLiveMetrics)
    {
        var snap = includeLiveMetrics ? _metrics.Snapshot() : null;
        long totalRequests = 0;
        if (snap != null)
        {
            foreach (var ep in endpoints)
            {
                if (snap.TryGetValue(ep.Id, out var m))
                    totalRequests += Interlocked.Read(ref m.Requests);
            }
        }

        return endpoints.Select(e =>
        {
            var dto = new TseGatewayEndpointDto
            {
                Id = e.Id,
                Provider = e.Provider,
                Endpoint = e.EndpointUrl,
                Weight = e.Weight,
                Enabled = e.Enabled,
                SortOrder = e.SortOrder,
                Status = "unknown",
            };

            if (snap != null && snap.TryGetValue(e.Id, out var metrics))
            {
                var req = Interlocked.Read(ref metrics.Requests);
                var ok = Interlocked.Read(ref metrics.SuccessCount);
                var fail = Interlocked.Read(ref metrics.FailureCount);
                var ms = Interlocked.Read(ref metrics.TotalResponseMs);
                dto.Requests = req;
                dto.SuccessCount = ok;
                dto.FailureCount = fail;
                dto.AvgResponseTimeMs = req == 0 ? 0 : Math.Round((double)ms / req, 2);
                dto.Load = totalRequests == 0 ? 0 : (int)Math.Round(100.0 * req / totalRequests);
                dto.LastCheckedAt = metrics.LastCheckedAtUtc;
                dto.Status = metrics.Healthy switch
                {
                    true => "healthy",
                    false => "unhealthy",
                    _ => "unknown",
                };
            }

            return dto;
        }).ToList();
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
